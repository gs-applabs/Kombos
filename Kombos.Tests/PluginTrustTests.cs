using System;
using System.IO;
using Kombos.Services;
using Xunit;

namespace Kombos.Tests
{
    public class PluginTrustTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "kombos-tests-" + Guid.NewGuid().ToString("N"));
        public PluginTrustTests() => Directory.CreateDirectory(_root);
        public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

        private string Make(string parent, string name, params (string file, string content)[] files)
        {
            var dir = Path.Combine(_root, parent, name);
            Directory.CreateDirectory(dir);
            foreach (var (f, c) in files) File.WriteAllText(Path.Combine(dir, f), c);
            return dir;
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> Empty = new();

        [Fact]
        public void UnknownPlugin_IsNotTrusted()
        {
            var dir = Make("user", "Evil", ("Evil.dll", "x"));
            var r = PluginTrust.Evaluate(dir, Empty, Path.Combine(_root, "bundled"));
            Assert.Equal(PluginTrustState.Unknown, r.State);
            Assert.Equal(64, r.Hash.Length);
        }

        [Fact]
        public void SameAsBundled_IsTrusted_WithoutAnyApproval()
        {
            Make("bundled", "Weather", ("W.dll", "same"));
            var dir = Make("user", "Weather", ("W.dll", "same"));
            Assert.Equal(PluginTrustState.Trusted, PluginTrust.Evaluate(dir, Empty, Path.Combine(_root, "bundled")).State);
        }

        [Fact]
        public void TamperedCopyOfBundled_IsNotTrusted()
        {
            Make("bundled", "Weather", ("W.dll", "same"));
            var dir = Make("user", "Weather", ("W.dll", "TAMPERED"));
            Assert.NotEqual(PluginTrustState.Trusted, PluginTrust.Evaluate(dir, Empty, Path.Combine(_root, "bundled")).State);
        }

        [Fact]
        public void ApprovedHash_IsTrusted_ChangedHash_NeedsNewApproval()
        {
            var dir = Make("user", "Mine", ("Mine.dll", "v1"));
            var store = new PluginAllowListStore(Path.Combine(_root, "allow.json"));
            store.Set("Mine", PluginTrust.FolderHash(dir));
            Assert.Equal(PluginTrustState.Trusted, PluginTrust.Evaluate(dir, store.Load(), null).State);
            File.WriteAllText(Path.Combine(dir, "Mine.dll"), "v2");
            Assert.Equal(PluginTrustState.Changed, PluginTrust.Evaluate(dir, store.Load(), null).State);
        }

        [Fact]
        public void AnExtraDependencyDll_ChangesTheFolderHash()
        {
            var dir = Make("user", "Dep", ("A.dll", "a"));
            var before = PluginTrust.FolderHash(dir);
            File.WriteAllText(Path.Combine(dir, "Planted.dll"), "payload");   // a dependency dropped next to an approved plugin
            Assert.NotEqual(before, PluginTrust.FolderHash(dir));
            var store = new PluginAllowListStore(Path.Combine(_root, "allow2.json"));
            store.Set("Dep", before);
            Assert.Equal(PluginTrustState.Changed, PluginTrust.Evaluate(dir, store.Load(), null).State);
        }

        [Fact]
        public void AllowListStore_RoundTrips_AndSurvivesCorruptFile()
        {
            var p = Path.Combine(_root, "allow3.json");
            var s = new PluginAllowListStore(p);
            s.Set("X", "abc");
            Assert.Equal("abc", new PluginAllowListStore(p).Load()["x"]);   // το όνομα φακέλου δεν ξεχωρίζει πεζά/κεφαλαία (Windows)
            File.WriteAllText(p, "{broken");
            Assert.Empty(new PluginAllowListStore(p).Load());
        }

        [Fact]
        public void EmptyFolder_HasNoHash()
        {
            Assert.Equal("", PluginTrust.FolderHash(Make("user", "Empty")));
        }
    }
}
