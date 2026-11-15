using System;
using System.IO;
using Kombos.Services;
using Xunit;

namespace Kombos.Tests
{
    /// <summary>Έλεγχοι σκληραγώγησης του Nexus Marketplace: επικύρωση URL και προστασία από path traversal στα ονόματα plugin.</summary>
    public class PluginMarketplaceTests
    {
        [Theory]
        [InlineData("https://github.com/owner/repo", "owner", "repo")]
        [InlineData("https://github.com/owner/repo/", "owner", "repo")]
        [InlineData("https://github.com/owner/repo.git", "owner", "repo")]
        [InlineData("https://www.github.com/Some-Owner/my_plugin.v2", "Some-Owner", "my_plugin.v2")]
        [InlineData("  https://github.com/owner/repo/tree/main  ", "owner", "repo")]
        public void ParseGitHubUrl_AcceptsNormalRepoUrls(string url, string owner, string repo)
        {
            var (o, r) = PluginMarketplace.ParseGitHubUrl(url);
            Assert.Equal(owner, o);
            Assert.Equal(repo, r);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not a url")]
        [InlineData("http://github.com/owner/repo")]          // όχι https
        [InlineData("https://evil.example.com/owner/repo")]   // άλλος host
        [InlineData("https://github.com.evil.example/owner/repo")]
        [InlineData("https://github.com/owner")]              // λείπει το repo
        [InlineData("https://github.com/owner/%2e%2e")]       // κωδικοποιημένο ".."
        [InlineData("https://github.com/owner/re%20po")]      // μη επιτρεπτός χαρακτήρας
        [InlineData("ftp://github.com/owner/repo")]
        public void ParseGitHubUrl_RejectsUnsafeInput(string url)
        {
            Assert.Throws<InvalidOperationException>(() => PluginMarketplace.ParseGitHubUrl(url));
        }

        [Theory]
        [InlineData("..")]
        [InlineData(".")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("a/b")]
        [InlineData(@"a\b")]
        [InlineData(@"..\..\Windows")]
        [InlineData("../outside")]
        [InlineData(@"C:\Windows")]
        public void ResolveUnder_RejectsTraversalAndNonNames(string name)
        {
            var root = Path.Combine(Path.GetTempPath(), "kombos-plugins-root");
            Assert.Throws<ArgumentException>(() => PluginLoader.ResolveUnder(root, name));
        }

        [Fact]
        public void ResolveUnder_ReturnsPathInsideRootForSimpleNames()
        {
            var root = Path.Combine(Path.GetTempPath(), "kombos-plugins-root");
            var p = PluginLoader.ResolveUnder(root, "KombosWeather");
            Assert.Equal(Path.Combine(Path.GetFullPath(root), "KombosWeather"), p);
            Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, p, StringComparison.OrdinalIgnoreCase);
        }
    }
}
