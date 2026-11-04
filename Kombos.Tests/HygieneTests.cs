using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Kombos.Localization;
using Xunit;

namespace Kombos.Tests
{
    /// <summary>Υγιεινή έκδοσης: πληροφορίες έκδοσης σε κάθε binary μας, κανένα ίχνος του μηχανήματος build ή προσωπικά δεδομένα
    /// μέσα στα binaries και στα tracked αρχεία, καμία κρυφή PowerShell, Inno Setup 7 guard και ενιαίος εκδότης στον installer.</summary>
    public class HygieneTests
    {
        private const string Publisher = "giorstergiopoulos-design";

        // ίδιος κατάλογος με το trace scan του security-scan workflow
        private static readonly string[] Traces = { @"C:\Users\", @"C:\w\", @"D:\a\", "gstrj", "LOGISTIRIO", "giorstergiopoulos@gmail", "/Users/" };

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Kombos.csproj"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }

        private static IEnumerable<string> OurBinaries()
        {
            var root = RepoRoot();
#if !DEBUG
            // Debug builds carry full paths by design (PDB); the Release build (and everything we ship) must be clean
            yield return typeof(L).Assembly.Location;
#endif
            foreach (var f in Directory.GetFiles(Path.Combine(root, "PluginsBundled"), "*.dll", SearchOption.AllDirectories)) yield return f;
            foreach (var f in Directory.GetFiles(Path.Combine(root, "plugins-src", "_ref"), "*.dll")) yield return f;
            var publish = Path.Combine(root, "publish");
            if (Directory.Exists(publish))
                foreach (var f in Directory.GetFiles(publish).Where(f => Path.GetFileName(f).StartsWith("Kombos", StringComparison.OrdinalIgnoreCase) && (f.EndsWith(".dll") || f.EndsWith(".exe")))) yield return f;
        }

        // Το apphost (Kombos.exe) είναι πρότυπο της Microsoft και φέρει το δικό της debug path "D:\a\_work\1\s\..." - δεν είναι ίχνος δικό μας.
        private const string MicrosoftApphostPath = @"D:\a\_work\";

        private static IEnumerable<string> TraceHits(byte[] bytes)
        {
            var latin = Encoding.Latin1.GetString(bytes).Replace(MicrosoftApphostPath, "", StringComparison.OrdinalIgnoreCase);
            var utf16 = Encoding.Unicode.GetString(bytes).Replace(MicrosoftApphostPath, "", StringComparison.OrdinalIgnoreCase);
            foreach (var p in Traces)
                if (latin.Contains(p, StringComparison.OrdinalIgnoreCase) || utf16.Contains(p, StringComparison.OrdinalIgnoreCase)) yield return p;
        }

        [Fact]
        public void OurBinaries_CarryNoBuildPathsOrPersonalData()
        {
            var hits = new List<string>();
            foreach (var f in OurBinaries())
                foreach (var h in TraceHits(File.ReadAllBytes(f))) hits.Add($"{Path.GetFileName(f)} contains '{h}'");
            Assert.True(hits.Count == 0, string.Join("\n", hits));
        }

        [Fact]
        public void NoPdbFilesAreShipped()
        {
            var root = RepoRoot();
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "PluginsBundled"), "*.pdb", SearchOption.AllDirectories));
            var publish = Path.Combine(root, "publish");
            if (Directory.Exists(publish)) Assert.Empty(Directory.GetFiles(publish, "*.pdb"));
        }

        [Fact]
        public void OurBinaries_HaveVersionInformationWithTheSamePublisher()
        {
            var checkedAny = false;
            foreach (var f in OurBinaries().Where(f => !f.EndsWith("Kombos.dll") || f.Contains("bin") || true))
            {
                var vi = FileVersionInfo.GetVersionInfo(f);
                var name = Path.GetFileName(f);
                Assert.Equal(Publisher, vi.CompanyName);
                Assert.Equal("Kombos", vi.ProductName);
                Assert.False(string.IsNullOrWhiteSpace(vi.FileDescription), name + " has no description");
                Assert.Contains("giorstergiopoulos-design", vi.LegalCopyright);
                checkedAny = true;
            }
            Assert.True(checkedAny);
            var main = FileVersionInfo.GetVersionInfo(typeof(L).Assembly.Location);
            Assert.Equal(L.AppVersion + ".0", main.FileVersion);
            Assert.StartsWith(L.AppVersion, main.ProductVersion);
        }

        [Fact]
        public void TrackedTextFiles_HaveNoPersonalPathsOrEmail()
        {
            var root = RepoRoot();
            var skipDirs = new[] { ".git", "bin", "obj", "publish", "release", "output", ".vs" };
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".md", ".cs", ".ps1", ".yml", ".iss", ".json", ".xaml", ".csproj", ".props", ".pas", ".bat", ".txt" };
            // these two legitimately NAME the patterns they look for
            var self = new[] { "HygieneTests.cs", "security-scan.yml" };
            var hits = new List<string>();
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, f);
                if (rel.Split(Path.DirectorySeparatorChar).Any(p => skipDirs.Contains(p, StringComparer.OrdinalIgnoreCase))) continue;
                if (!exts.Contains(Path.GetExtension(f)) || self.Contains(Path.GetFileName(f))) continue;
                var text = File.ReadAllText(f);
                foreach (var p in Traces) if (text.Contains(p, StringComparison.OrdinalIgnoreCase)) hits.Add($"{rel}: '{p}'");
            }
            Assert.True(hits.Count == 0, string.Join("\n", hits));
        }

        [Fact]
        public void NoHiddenPowerShell_InCodeOrInstaller()
        {
            var root = RepoRoot();
            var bad = new[] { "-ExecutionPolicy", "-WindowStyle Hidden", "powershell.exe", "pwsh.exe" };
            var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.EndsWith("HygieneTests.cs"))
                .Concat(new[] { Path.Combine(root, "installer", "Kombos.iss") });
            var hits = new List<string>();
            foreach (var f in files)
            {
                var text = File.ReadAllText(f);
                foreach (var b in bad) if (text.Contains(b, StringComparison.OrdinalIgnoreCase)) hits.Add($"{Path.GetRelativePath(root, f)}: '{b}'");
            }
            Assert.True(hits.Count == 0, string.Join("\n", hits));
        }

        [Fact]
        public void Installer_RequiresInno7_HasPublisherAndVersionInfo()
        {
            var iss = File.ReadAllText(Path.Combine(RepoRoot(), "installer", "Kombos.iss"));
            Assert.Contains("#if Ver < EncodeVer(7, 0, 0)", iss);
            Assert.Contains("#error", iss);
            Assert.Contains("#define MyAppPublisher \"" + Publisher + "\"", iss);
            foreach (var key in new[] { "AppPublisherURL=", "AppSupportURL=", "VersionInfoCompany={#MyAppPublisher}", "VersionInfoDescription=", "VersionInfoVersion=", "VersionInfoProductName=", "VersionInfoCopyright=" })
                Assert.Contains(key, iss);
            var ci = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "ci-install-inno.ps1"));
            Assert.Contains("Pyrsys B.V.", ci);
            Assert.Contains("Get-FileHash", ci);
        }
    }
}
