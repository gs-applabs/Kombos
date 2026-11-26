using System;
using Kombos.Services;
using Xunit;

namespace Kombos.Tests
{
    public class SecurityReview499Tests
    {
        [Theory]
        [InlineData("https://example.com/x", true)]
        [InlineData("http://localhost:8080/", true)]
        [InlineData("file:///C:/Windows/System32/calc.exe", false)]
        [InlineData("ms-msdt:/id PCWDiagnostic", false)]
        [InlineData("search-ms:query=x", false)]
        [InlineData("calc.exe", false)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void OnlyHttpAndHttpsLinksAreOpened(string? url, bool expected) => Assert.Equal(expected, UrlSafety.IsSafeWebUrl(url));

        [Fact]
        public void ZipBombLimitsAreEnforced()
        {
            PluginMarketplace.CheckZipLimits(10, 1024);
            Assert.Throws<InvalidOperationException>(() => PluginMarketplace.CheckZipLimits(10, PluginMarketplace.MaxUncompressedBytes + 1));
            Assert.Throws<InvalidOperationException>(() => PluginMarketplace.CheckZipLimits(PluginMarketplace.MaxEntries + 1, 1));
        }
    }
}
