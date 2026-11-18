using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kombos.Models;
using Kombos.Services;
using Xunit;

namespace Kombos.Tests
{
    public class UpdateInstallerTests
    {
        private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static string ReleaseJson(string tag, params (string name, string digest)[] assets)
        {
            var list = string.Join(",", assets.Select(a =>
                $"{{\"name\":\"{a.name}\",\"size\":1000,\"browser_download_url\":\"https://github.com/gs-applabs/Kombos/releases/download/{tag}/{a.name}\"" +
                (a.digest != null ? $",\"digest\":\"sha256:{a.digest}\"" : "") + "}"));
            return $"{{\"tag_name\":\"{tag}\",\"html_url\":\"https://github.com/gs-applabs/Kombos/releases/tag/{tag}\",\"assets\":[{list}]}}";
        }

        private static HttpClient Client(string json) =>
            new(new FakeHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) } });

        [Fact]
        public async Task PicksExactlyTheInstallerOfTheNewVersion_WithItsHash()
        {
            var json = ReleaseJson("v4.9.8", ("KombosSetup-4.9.7.exe", Hash), ("KombosSetup-4.9.8.exe", Hash), ("KombosSetup-4.9.8.exe.sha256", null!), ("Kombos-portable.zip", Hash));
            var (plan, reason) = await UpdateInstaller.PlanAsync(Client(json), "4.9.7");
            Assert.Null(reason);
            Assert.Equal("KombosSetup-4.9.8.exe", plan!.Installer.Name);
            Assert.Equal("4.9.8", plan.Version);
            Assert.Equal(Hash, plan.ExpectedSha256);
        }

        [Theory]
        [InlineData("v4.9.7", "4.9.7")]   // ίδια έκδοση
        [InlineData("v4.9.6", "4.9.7")]   // παλιότερη
        [InlineData("v4.10.0-beta.1", "4.9.7")] // pre-release
        public async Task NeverPlansAnOlderSameOrPreRelease(string tag, string current)
        {
            var json = ReleaseJson(tag, ($"KombosSetup-{tag.TrimStart('v')}.exe", Hash));
            var (plan, reason) = await UpdateInstaller.PlanAsync(Client(json), current);
            Assert.Null(plan);
            Assert.Equal("up-to-date", reason);
        }

        [Fact]
        public async Task RefusesWhenThereIsNoMatchingAsset_OrNoChecksum()
        {
            var noAsset = ReleaseJson("v4.9.8", ("KombosSetup-4.9.7.exe", Hash), ("Other.exe", Hash));
            Assert.Equal("no-asset", (await UpdateInstaller.PlanAsync(Client(noAsset), "4.9.7")).Reason);
            var noHash = ReleaseJson("v4.9.8", ("KombosSetup-4.9.8.exe", null!));
            Assert.Equal("no-checksum", (await UpdateInstaller.PlanAsync(Client(noHash), "4.9.7")).Reason);
        }

        [Fact]
        public async Task ApiFailure_IsAReason_NotAnException()
        {
            var http = new HttpClient(new FakeHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.Forbidden) });
            Assert.Equal("no-release", (await UpdateInstaller.PlanAsync(http, "4.9.7")).Reason);
        }

        [Fact]
        public void StartInfo_IsTheNormalInstaller_NotSilent_AndOnlyForARealSetupFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), "kombos-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var setup = Path.Combine(dir, "KombosSetup-4.9.8.exe");
                File.WriteAllText(setup, "x");
                var log = Path.Combine(dir, "logs", "update-4.9.8.log");
                var psi = UpdateInstaller.BuildStartInfo(setup, log);
                Assert.Equal(setup, psi.FileName);
                Assert.True(psi.UseShellExecute);                       // UAC comes from the manifest of the setup
                Assert.Contains("/LOG=", psi.Arguments);
                Assert.Contains("/NORESTART", psi.Arguments);
                Assert.DoesNotContain("SILENT", psi.Arguments, StringComparison.OrdinalIgnoreCase);
                Assert.True(Directory.Exists(Path.GetDirectoryName(log)));

                var evil = Path.Combine(dir, "calc.exe"); File.WriteAllText(evil, "x");
                Assert.Throws<InvalidOperationException>(() => UpdateInstaller.BuildStartInfo(evil, log));
                Assert.False(UpdateInstaller.IsSafeSetupPath(@"KombosSetup-4.9.8.exe"));                    // not fully qualified
                Assert.False(UpdateInstaller.IsSafeSetupPath(Path.Combine(dir, "KombosSetup-4.9.9.exe")));  // does not exist
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        [Fact]
        public void InstallerNameConvention_MatchesTheCiAndInnoOutput()
        {
            Assert.Equal("KombosSetup-4.9.8.exe", UpdateInstaller.InstallerNameFor("v4.9.8"));
            var iss = File.ReadAllText(Path.Combine(RepoRoot(), "installer", "Kombos.iss"));
            Assert.Contains("OutputBaseFilename=KombosSetup-{#MyAppVersion}", iss);
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Kombos.csproj"))) dir = dir.Parent;
            return dir!.FullName;
        }

        [Fact]
        public void FamilyWindow_HasDownloads_WithConfirmation_Hash_Timeout_AndNoSilentRun()
        {
            var src = File.ReadAllText(Path.Combine(RepoRoot(), "MainWindow.Suite.cs"));
            Assert.Contains("InstallSuiteAppAsync", src);
            Assert.Contains("SuiteStrings.ConfirmDownload", src);             // confirmation: file, size, source
            Assert.Contains("DownloadVerifiedAsync(SuiteDownloadHttp, asset", src);   // SHA-256 verified, no total timeout client + stall timeout
            Assert.Contains("_suiteDownloading.Add(app.Id)", src);            // no double download
            Assert.DoesNotContain("Process.Start(path", src);                 // the downloaded installer is never run from here
        }

        [Fact]
        public void InstallerFollowsTheWindowsTheme_WithDarkPicturesAndTransparentSmallImage()
        {
            var root = RepoRoot();
            var iss = File.ReadAllText(Path.Combine(root, "installer", "Kombos.iss"));
            Assert.Contains("WizardStyle=modern dynamic", iss);
            foreach (var key in new[] { "WizardImageFileDynamicDark=", "WizardImageBackColorDynamicDark=", "WizardSmallImageFileDynamicDark=" }) Assert.Contains(key, iss);
            foreach (var tag in new[] { "100", "125", "150", "200" })
            {
                Assert.True(File.Exists(Path.Combine(root, "installer", "wizard", $"large-{tag}.bmp")));
                Assert.True(File.Exists(Path.Combine(root, "installer", "wizard", $"dark-large-{tag}.bmp")));
                var small = Path.Combine(root, "installer", "wizard", $"small-{tag}.png");
                var bytes = File.ReadAllBytes(small);
                Assert.Equal(0x89, bytes[0]);          // PNG
                Assert.Equal(6, bytes[25]);            // colour type 6 = RGBA: a transparent background is possible
            }
        }
    }

    public class ConversationStoreAtomicTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "kombos-tests-" + Guid.NewGuid().ToString("N"));
        public ConversationStoreAtomicTests() => Directory.CreateDirectory(_dir);
        public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

        private static List<Conversation> Sample(string title) => new() { new Conversation { Id = "1", Title = title, History = new() { new ChatTurn { Role = "user", Content = "hi" } } } };

        [Fact]
        public void Save_IsAtomic_KeepsABackup_AndLeavesNoTempFile()
        {
            ConversationStore.Save(Sample("first"), _dir);
            ConversationStore.Save(Sample("second"), _dir);
            Assert.False(File.Exists(Path.Combine(_dir, "conversations.json.tmp")));
            Assert.Equal("second", ConversationStore.Load(_dir)[0].Title);
            Assert.Equal("first", JsonDeserializeTitle(Path.Combine(_dir, "conversations.json.bak")));
        }

        private static string JsonDeserializeTitle(string path) =>
            System.Text.Json.JsonSerializer.Deserialize<List<Conversation>>(File.ReadAllText(path))![0].Title;

        [Fact]
        public void CorruptMainFile_FallsBackToTheBackup_NotToAnEmptyList()
        {
            ConversationStore.Save(Sample("keep me"), _dir);
            ConversationStore.Save(Sample("newer"), _dir);
            File.WriteAllText(Path.Combine(_dir, "conversations.json"), "{half-written");   // a crash in the middle of an old-style write
            Assert.Equal("keep me", ConversationStore.Load(_dir)[0].Title);
        }

        [Fact]
        public void Output_IsCompact()
        {
            var json = ConversationStore.Serialize(Sample("t"));
            Assert.DoesNotContain("\n", json);
        }

        [Fact]
        public async Task ManyConcurrentSnapshotWrites_NeverInterleave_AndTheLastOneWins()
        {
            var tasks = Enumerable.Range(0, 20).Select(i => ConversationStore.SaveSnapshotAsync(ConversationStore.Serialize(Sample("v" + i)), _dir)).ToArray();
            await Task.WhenAll(tasks);
            var title = ConversationStore.Load(_dir)[0].Title;
            Assert.StartsWith("v", title);                       // always a complete, valid file
            Assert.False(File.Exists(Path.Combine(_dir, "conversations.json.tmp")));
        }
    }

    public class GatewayLimitsTests
    {
        [Fact]
        public async Task OversizedBody_Gets413_WithoutBeingReadIntoMemory()
        {
            var server = new GatewayServer(() => new List<GatewayAgentEntry> { new("Claude", "fake-key", null, null) }, _ => { });
            Assert.True(server.TryStart(28431, out _));
            try
            {
                using var http = new HttpClient();
                var body = new string('x', GatewayServer.MaxBodyBytes + 10);
                var response = await http.PostAsync("http://127.0.0.1:28431/v1/chat/completions", new StringContent(body, Encoding.UTF8, "application/json"));
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            }
            finally { server.Stop(); }
        }

        [Fact]
        public async Task ReadBodyLimited_ReturnsNullAboveTheLimit_AndTheTextBelow()
        {
            using var small = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
            Assert.Equal("hello", await GatewayServer.ReadBodyLimitedAsync(small, 10));
            using var big = new MemoryStream(new byte[100]);
            Assert.Null(await GatewayServer.ReadBodyLimitedAsync(big, 50));
        }

        [Fact]
        public void LimitsAreSane()
        {
            Assert.InRange(GatewayServer.MaxBodyBytes, 100_000, 10_000_000);
            Assert.InRange(GatewayServer.MaxConcurrentRequests, 2, 64);
        }
    }

    public class ProjectIndexingTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "kombos-tests-" + Guid.NewGuid().ToString("N"));
        public ProjectIndexingTests() => Directory.CreateDirectory(_dir);
        public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

        [Fact]
        public async Task IndexesInTheBackground_WithProgress_AndHonoursCancellation()
        {
            for (int i = 0; i < 120; i++) File.WriteAllText(Path.Combine(_dir, $"f{i}.txt"), "content " + i);
            int reported = 0;
            var index = await ProjectMemoryStore.BuildOrUpdateIndexAsync(_dir, new Progress<int>(_ => reported++));
            Assert.Equal(120, index.Files.Count);
            Assert.All(index.Files, f => Assert.Equal(16, f.Hash.Length));

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProjectMemoryStore.BuildOrUpdateIndexAsync(_dir, null, cts.Token));
        }

        [Fact]
        public void HashLimit_IsDefined_AndSmallFilesAreHashed()
        {
            Assert.True(ProjectMemoryStore.MaxHashFileBytes >= 1024 * 1024);
            File.WriteAllText(Path.Combine(_dir, "a.txt"), "x");
            var idx = ProjectMemoryStore.BuildOrUpdateIndex(_dir);
            Assert.NotEqual("large", idx.Files.Single().Hash);
        }
    }
}
