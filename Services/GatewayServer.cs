using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Kombos.Models;

namespace Kombos.Services
{
    public class GatewayRequestLogEntry
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string Agent { get; set; } = "";
        public bool Success { get; set; }
        public string Note { get; set; } = "";
    }

    /// <summary>Ένας agent στη σειρά προτεραιότητας δρομολόγησης - BaseUrl null σημαίνει Claude (χρησιμοποιεί
    /// τον δικό του client, όχι το γενικό OpenAI-compatible), αλλιώς OpenAI-compatible endpoint.</summary>
    public record GatewayAgentEntry(string Name, string ApiKey, string? BaseUrl, string? Model);

    /// <summary>"Κόμβος Gateway" (v4.6.0, ιδέα εμπνευσμένη από το OmniRoute - https://github.com/diegosouzapw/OmniRoute,
    /// MIT) - τοπική HTTP πύλη, OpenAI-compatible <c>/v1/chat/completions</c> endpoint. Δρομολογεί αυτόματα σε
    /// όποιο ήδη-αποθηκευμένο agent του Κόμβου είναι διαθέσιμο, με αυτόματο fallback στον επόμενο αν ο πρώτος
    /// αποτύχει (rate limit/quota/δίκτυο) - το ίδιο πνεύμα με το "automatic fallback across providers" του
    /// OmniRoute, αλλά ρυθμισμένο εξ ολοκλήρου μέσα από το ήδη υπάρχον, φιλικό GUI του Κόμβου (Ρυθμίσεις →
    /// Συμπεριφορά) αντί για .env αρχεία/CLI/Docker/ξεχωριστό dashboard.
    /// ΑΣΦΑΛΕΙΑ: ακούει ΜΟΝΟ σε 127.0.0.1 (loopback) - ποτέ "+" ή 0.0.0.0, άρα ποτέ προσβάσιμο από άλλη
    /// συσκευή στο δίκτυο. Δεν αντιγράφει κώδικα από το OmniRoute (MIT license θα το επέτρεπε, αλλά η
    /// αρχιτεκτονική/γλώσσα είναι εντελώς διαφορετική - Node.js εκεί, C#/.NET εδώ) - μόνο η ΙΔΕΑ της ενιαίας
    /// τοπικής πύλης με αυτόματο fallback υλοποιήθηκε από την αρχή.</summary>
    public class GatewayServer : IDisposable
    {
        private readonly Func<List<GatewayAgentEntry>> _getPriorityAgents;
        private readonly Action<GatewayRequestLogEntry> _onRequestLogged;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;

        public bool IsRunning => _listener?.IsListening == true;
        public int Port { get; private set; }

        public GatewayServer(Func<List<GatewayAgentEntry>> getPriorityAgents, Action<GatewayRequestLogEntry> onRequestLogged)
        {
            _getPriorityAgents = getPriorityAgents;
            _onRequestLogged = onRequestLogged;
        }

        public bool TryStart(int port, out string? error)
        {
            Stop();
            try
            {
                Port = port;
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                _listener.Start();
                _cts = new CancellationTokenSource();
                _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message; // π.χ. η θύρα χρησιμοποιείται ήδη από άλλη εφαρμογή
                _listener = null;
                return false;
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _listener?.Stop(); _listener?.Close(); } catch { /* ήδη σταματημένο */ }
            _listener = null;
        }

        public void Dispose() => Stop();

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener is { IsListening: true } listener)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); }
                catch { break; } // ο listener σταμάτησε (Stop() κλήθηκε)
                _ = Task.Run(() => HandleRequestAsync(ctx), ct);
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext ctx)
        {
            try
            {
                if (ctx.Request.Url?.AbsolutePath != "/v1/chat/completions" || ctx.Request.HttpMethod != "POST")
                {
                    ctx.Response.StatusCode = 404;
                    await WriteJsonAsync(ctx, new { error = "Μόνο POST /v1/chat/completions υποστηρίζεται." });
                    return;
                }

                // Προστασία από CSRF/κατάχρηση: μια ιστοσελίδα του browser δεν πρέπει να μπορεί να ξοδέψει τα αποθηκευμένα κλειδιά.
                if (CheckRequestSafety(ctx.Request.Headers["Origin"], ctx.Request.ContentType, ctx.Request.ContentLength64) is { } unsafeRequest)
                {
                    ctx.Response.StatusCode = unsafeRequest.Status;
                    await WriteJsonAsync(ctx, new { error = unsafeRequest.Error });
                    return;
                }

                // Ανάγνωση σώματος με πραγματικό όριο (το chunked αίτημα δεν έχει Content-Length, άρα μετράμε τα bytes που φτάνουν).
                byte[] bodyBytes;
                using (var ms = new MemoryStream())
                {
                    var chunk = new byte[16 * 1024];
                    int n;
                    while ((n = await ctx.Request.InputStream.ReadAsync(chunk, 0, chunk.Length)) > 0)
                    {
                        if (ms.Length + n > MaxBodyBytes)
                        {
                            ctx.Response.StatusCode = 413;
                            await WriteJsonAsync(ctx, new { error = "Το αίτημα είναι υπερβολικά μεγάλο." });
                            return;
                        }
                        ms.Write(chunk, 0, n);
                    }
                    bodyBytes = ms.ToArray();
                }
                string bodyText;
                using (var reader = new StreamReader(new MemoryStream(bodyBytes), Encoding.UTF8)) bodyText = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(bodyText);
                var root = doc.RootElement;

                var history = new List<ChatTurn>();
                var systemPrompt = "You are a helpful assistant.";
                if (root.TryGetProperty("messages", out var messagesEl) && messagesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in messagesEl.EnumerateArray())
                    {
                        var role = m.TryGetProperty("role", out var r) ? r.GetString() ?? "user" : "user";
                        var content = m.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                        if (role == "system") { systemPrompt = content; continue; }
                        history.Add(new ChatTurn { Role = role == "assistant" ? "assistant" : "user", Content = content });
                    }
                }

                if (history.Count == 0)
                {
                    ctx.Response.StatusCode = 400;
                    await WriteJsonAsync(ctx, new { error = "Λείπει τουλάχιστον ένα μήνυμα χρήστη." });
                    return;
                }

                var agents = _getPriorityAgents();
                if (agents.Count == 0)
                {
                    ctx.Response.StatusCode = 503;
                    await WriteJsonAsync(ctx, new { error = "Δεν υπάρχει κανένα agent με αποθηκευμένο κλειδί στο Κόμβος." });
                    return;
                }

                Exception? lastError = null;
                foreach (var agent in agents)
                {
                    try
                    {
                        var result = agent.BaseUrl == null
                            ? await new ClaudeClient { ApiKey = agent.ApiKey }.SendAsync(systemPrompt, history)
                            : await OpenAiCompatibleClient.SendAsync(agent.BaseUrl, agent.Model!, agent.ApiKey, systemPrompt, history);

                        _onRequestLogged(new GatewayRequestLogEntry { Agent = agent.Name, Success = true, Note = $"{history.Count} μηνύματα" });

                        await WriteJsonAsync(ctx, new
                        {
                            id = "kombos-" + Guid.NewGuid().ToString("N")[..12],
                            @object = "chat.completion",
                            model = agent.Name,
                            choices = new object[] { new { index = 0, message = new { role = "assistant", content = result.ReplyText }, finish_reason = "stop" } },
                            usage = new { prompt_tokens = result.InputTokens, completion_tokens = result.OutputTokens, total_tokens = result.InputTokens + result.OutputTokens }
                        });
                        return;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        _onRequestLogged(new GatewayRequestLogEntry { Agent = agent.Name, Success = false, Note = ex.Message });
                    }
                }

                ctx.Response.StatusCode = 502;
                await WriteJsonAsync(ctx, new { error = $"Όλα τα ρυθμισμένα agents απέτυχαν. Τελευταίο σφάλμα: {lastError?.Message}" });
            }
            catch (JsonException)
            {
                try
                {
                    ctx.Response.StatusCode = 400;
                    await WriteJsonAsync(ctx, new { error = "Μη έγκυρο JSON στο σώμα του αιτήματος." });
                }
                catch { /* η σύνδεση μπορεί να έχει ήδη κλείσει */ }
            }
            catch (Exception ex)
            {
                try
                {
                    ctx.Response.StatusCode = 500;
                    await WriteJsonAsync(ctx, new { error = ex.Message });
                }
                catch { /* η σύνδεση μπορεί να έχει ήδη κλείσει */ }
            }
        }

        /// <summary>Μέγιστο μέγεθος σώματος αιτήματος (4 MB - υπεραρκετό για συνομιλία, αποτρέπει εξάντληση μνήμης).</summary>
        public const long MaxBodyBytes = 4L * 1024 * 1024;

        /// <summary>Έλεγχος ασφάλειας ΠΡΙΝ διαβαστεί το σώμα. Επιστρέφει null αν το αίτημα είναι αποδεκτό, αλλιώς (κωδικός, μήνυμα).
        /// • Origin: οι browsers το στέλνουν σε κάθε cross-origin αίτημα· οι πελάτες API (curl, SDK, Open WebUI server-side) όχι. Αν υπάρχει → 403
        ///   (μια ιστοσελίδα δεν πρέπει να χρησιμοποιεί την τοπική πύλη). • Content-Type: μόνο application/json (οι OpenAI-συμβατοί πελάτες
        ///   το στέλνουν πάντα) - αναγκάζει τον browser σε preflight, που η πύλη δεν εγκρίνει ποτέ. • Content-Length: αν δηλώνεται, ≤ MaxBodyBytes.</summary>
        public static (int Status, string Error)? CheckRequestSafety(string? origin, string? contentType, long contentLength)
        {
            if (!string.IsNullOrEmpty(origin))
                return (403, "Αιτήματα από σελίδες browser (κεφαλίδα Origin) δεν επιτρέπονται στην τοπική πύλη.");
            if (contentType == null || !contentType.TrimStart().StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                return (415, "Απαιτείται Content-Type: application/json.");
            if (contentLength > MaxBodyBytes)
                return (413, "Το αίτημα είναι υπερβολικά μεγάλο.");
            return null;
        }

        private static async Task WriteJsonAsync(HttpListenerContext ctx, object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.OutputStream.Close();
        }
    }
}
