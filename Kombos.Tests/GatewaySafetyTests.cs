using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Kombos.Services;

using Xunit;

namespace Kombos.Tests
{
    /// <summary>Ασφάλεια του "Κόμβος Gateway": μια ιστοσελίδα (CSRF) δεν πρέπει να μπορεί να ξοδέψει τα αποθηκευμένα κλειδιά API.</summary>
    public class GatewaySafetyTests
    {
        [Fact]
        public void CheckRequestSafety_AcceptsNormalApiClient()
        {
            Assert.Null(GatewayServer.CheckRequestSafety(null, "application/json", 120));
            Assert.Null(GatewayServer.CheckRequestSafety("", "application/json; charset=utf-8", -1)); // chunked: άγνωστο μήκος
            Assert.Null(GatewayServer.CheckRequestSafety(null, "Application/JSON", GatewayServer.MaxBodyBytes));
        }

        [Fact]
        public void CheckRequestSafety_RejectsBrowserOrigin()
        {
            var r = GatewayServer.CheckRequestSafety("https://evil.example", "application/json", 10);
            Assert.NotNull(r);
            Assert.Equal(403, r!.Value.Status);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("text/plain")]
        [InlineData("application/x-www-form-urlencoded")]
        [InlineData("multipart/form-data; boundary=x")]
        public void CheckRequestSafety_RejectsNonJsonContentType(string? contentType)
        {
            var r = GatewayServer.CheckRequestSafety(null, contentType, 10);
            Assert.NotNull(r);
            Assert.Equal(415, r!.Value.Status);
        }

        [Fact]
        public void CheckRequestSafety_RejectsOversizedBody()
        {
            var r = GatewayServer.CheckRequestSafety(null, "application/json", GatewayServer.MaxBodyBytes + 1);
            Assert.NotNull(r);
            Assert.Equal(413, r!.Value.Status);
        }

        [Fact]
        public async Task Request_WithOriginHeader_Returns403_EvenWithAgentsConfigured()
        {
            var server = new GatewayServer(() => new List<GatewayAgentEntry> { new("Claude", "fake-key", null, null) }, _ => { });
            Assert.True(server.TryStart(28431, out _));
            try
            {
                using var http = new HttpClient();
                using var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:28431/v1/chat/completions")
                {
                    Content = new StringContent("{\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}", Encoding.UTF8, "application/json")
                };
                req.Headers.TryAddWithoutValidation("Origin", "https://evil.example");
                var response = await http.SendAsync(req);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
            finally { server.Stop(); }
        }

        [Fact]
        public async Task Request_WithTextPlain_Returns415()
        {
            var server = new GatewayServer(() => new List<GatewayAgentEntry> { new("Claude", "fake-key", null, null) }, _ => { });
            Assert.True(server.TryStart(28432, out _));
            try
            {
                using var http = new HttpClient();
                var response = await http.PostAsync("http://127.0.0.1:28432/v1/chat/completions",
                    new StringContent("{\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}", Encoding.UTF8, "text/plain"));
                Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            }
            finally { server.Stop(); }
        }

        [Fact]
        public async Task InvalidJson_Returns400()
        {
            var server = new GatewayServer(() => new List<GatewayAgentEntry>(), _ => { });
            Assert.True(server.TryStart(28433, out _));
            try
            {
                using var http = new HttpClient();
                var response = await http.PostAsync("http://127.0.0.1:28433/v1/chat/completions",
                    new StringContent("{not json", Encoding.UTF8, "application/json"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            finally { server.Stop(); }
        }
    }
}
