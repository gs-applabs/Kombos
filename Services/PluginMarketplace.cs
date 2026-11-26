using System.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Kombos.Services
{
    /// <summary>Nexus Marketplace (v4.5.0) - εγκατάσταση plugin από Git URL, ΧΩΡΙΣ να χρειάζεται git.exe
    /// εγκατεστημένο: κατεβάζει το ZIP snapshot του branch μέσω του δημόσιου GitHub codeload endpoint
    /// (δοκιμάζει "main" μετά "master"), το αποσυμπιέζει σε προσωρινό φάκελο, βρίσκει τα .dll που ήδη
    /// υπάρχουν μέσα (το Kombos ΔΕΝ μεταγλωττίζει πηγαίο κώδικα - το plugin πρέπει να έχει ήδη build output
    /// στο repo, ίδιος κανόνας με την υπάρχουσα "Λήψη &amp; Ενσωμάτωση" γκαλερί) και τα αντιγράφει στο
    /// PluginLoader.PluginsRoot - ίδιο μοντέλο εμπιστοσύνης με ένα χειροκίνητο αντίγραφο DLL, απλά πιο
    /// βολικό. Ο χρήστης δίνει ρητά το URL - το Kombos δεν κατεβάζει/εγκαθιστά ποτέ τίποτα μόνο του.
    ///
    /// Σκληραγώγηση: μόνο https://github.com/owner/repo (όχι άλλοι host), αυστηρός έλεγχος owner/repo
    /// (καμία διαδρομή/".."), timeout και όριο μεγέθους λήψης, και καθαρισμός του προσωρινού φακέλου ακόμη και σε σφάλμα.</summary>
    public static class PluginMarketplace
    {
        /// <summary>Μέγιστο μέγεθος ZIP που δέχεται το marketplace (προστασία από εξάντληση μνήμης/δίσκου).</summary>
        public const long MaxZipBytes = 100L * 1024 * 1024;

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };
        private static readonly Regex NamePart = new(@"^[A-Za-z0-9._-]{1,100}$", RegexOptions.Compiled);

        public static async Task<string> InstallFromGitHubAsync(string repoUrl)
        {
            var (owner, repo) = ParseGitHubUrl(repoUrl);
            byte[]? zipBytes = null;
            foreach (var branch in new[] { "main", "master" })
            {
                try
                {
                    zipBytes = await DownloadLimitedAsync($"https://codeload.github.com/{owner}/{repo}/zip/refs/heads/{branch}");
                    break;
                }
                catch (HttpRequestException) { /* δοκίμασε το επόμενο branch (π.χ. 404) */ }
            }
            if (zipBytes == null) throw new InvalidOperationException("Δεν βρέθηκε κλαδί main/master σε αυτό το repo.");

            var tempDir = Path.Combine(Path.GetTempPath(), "kombos-marketplace-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                var zipPath = Path.Combine(tempDir, "repo.zip");
                await File.WriteAllBytesAsync(zipPath, zipBytes);
                // Το ExtractToDirectory του .NET απορρίπτει εγγραφές που βγαίνουν εκτός φακέλου προορισμού (zip-slip).
                // Zip-bomb guard: a 100 MB zip may expand to many GB, so cap the declared uncompressed size and the entry count first.
                using (var zip = ZipFile.OpenRead(zipPath))
                    CheckZipLimits(zip.Entries.Count, zip.Entries.Sum(e => e.Length));
                ZipFile.ExtractToDirectory(zipPath, Path.Combine(tempDir, "x"));

                var dlls = Directory.GetFiles(Path.Combine(tempDir, "x"), "*.dll", SearchOption.AllDirectories);
                if (dlls.Length == 0)
                    throw new InvalidOperationException("Δεν βρέθηκαν .dll αρχεία στο repo - το Kombos δεν μεταγλωττίζει πηγαίο κώδικα, χρειάζεται ήδη-χτισμένο plugin.");

                PluginLoader.InstallFromDllFiles(repo, dlls);
                return repo;
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* καθαρισμός best-effort */ }
            }
        }

        public const long MaxUncompressedBytes = 500L * 1024 * 1024;
        public const int MaxEntries = 20000;

        public static void CheckZipLimits(int entryCount, long uncompressedBytes)
        {
            if (entryCount > MaxEntries || uncompressedBytes > MaxUncompressedBytes)
                throw new InvalidOperationException("Το αρχείο ZIP ξεπερνά τα όρια αποσυμπίεσης (500 MB / 20000 αρχεία).");
        }

        /// <summary>Κατεβάζει το URL με όριο <see cref="MaxZipBytes"/> (πρώτα από την κεφαλίδα Content-Length, μετά με μέτρηση ροής).</summary>
        private static async Task<byte[]> DownloadLimitedAsync(string url)
        {
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            if (resp.Content.Headers.ContentLength is long declared && declared > MaxZipBytes)
                throw new InvalidOperationException("Το repo είναι μεγαλύτερο από το επιτρεπτό όριο (100 MB).");

            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                if (ms.Length + read > MaxZipBytes)
                    throw new InvalidOperationException("Το repo είναι μεγαλύτερο από το επιτρεπτό όριο (100 MB).");
                ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
        }

        /// <summary>Δέχεται ΜΟΝΟ https://github.com/owner/repo (προαιρετικά με .git ή /). Επιστρέφει (owner, repo) ή ρίχνει InvalidOperationException.</summary>
        public static (string Owner, string Repo) ParseGitHubUrl(string url)
        {
            const string expected = "Αναμένεται URL μορφής https://github.com/owner/repo";
            url = (url ?? "").Trim().TrimEnd('/');
            if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) url = url[..^4];
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException(expected);
            if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
                !uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Επιτρέπονται μόνο repos του github.com.");
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length < 2) throw new InvalidOperationException(expected);
            var owner = segments[0];
            var repo = segments[1];
            if (!IsSafeName(owner) || !IsSafeName(repo))
                throw new InvalidOperationException("Μη έγκυρο όνομα owner/repo.");
            return (owner, repo);
        }

        private static bool IsSafeName(string s) => NamePart.IsMatch(s) && s.Trim('.').Length > 0;
    }
}
