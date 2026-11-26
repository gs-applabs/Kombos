using System;

namespace Kombos.Services
{
    /// <summary>4.9.9 (security review): URLs handed to ShellExecute must be plain web links. Tool/plugin/catalog data is not trusted
    /// to be free of file:, ms-msdt:, search-ms: or other protocol-handler URLs.</summary>
    public static class UrlSafety
    {
        public static bool IsSafeWebUrl(string? url) =>
            !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) &&
            (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) && !string.IsNullOrEmpty(u.Host);
    }
}
