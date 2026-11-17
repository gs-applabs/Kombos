using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Kombos.Services
{
    public record UpdatePlan(string Version, ReleaseAsset Installer, string ExpectedSha256, string ReleaseUrl);

    /// <summary>Ενημέρωση του ίδιου του Kombos end-to-end: σωστό asset του release (<c>KombosSetup-&lt;έκδοση&gt;.exe</c>, ΑΥΣΤΗΡΑ το όνομα της
    /// νέας έκδοσης), SHA-256 (αρχείο .sha256 ή digest του GitHub - χωρίς hash ΔΕΝ γίνεται λήψη), λήψη με timeout/ακύρωση, και εκκίνηση του
    /// κανονικού installer (UAC του Windows, ίδιο AppId → αναβάθμιση στο Program Files, log στο %AppData%\Kombos\logs). Ο installer ΔΕΝ ξεκινά
    /// ποτέ σιωπηλά και ΔΕΝ γίνεται ποτέ taskkill του δέντρου διεργασιών (ο installer είναι παιδί της εφαρμογής: ένα /T θα τον σκότωνε μαζί της).</summary>
    public static class UpdateInstaller
    {
        public const string Repo = "Kombos";
        private static readonly Regex SetupNameRx = new(@"^KombosSetup-\d+\.\d+\.\d+\.exe$", RegexOptions.CultureInvariant);

        public static string InstallerNameFor(string version) => $"KombosSetup-{version.TrimStart('v', 'V')}.exe";

        public static string UpdateDir => Path.Combine(Path.GetTempPath(), "Kombos-update");

        public static string LogPath(string version) => Path.Combine(AppPaths.Root, "logs", $"update-{version.TrimStart('v', 'V')}.log");

        /// <summary>Αποτέλεσμα σχεδιασμού: ή σχέδιο ή ο λόγος ("up-to-date", "no-release", "no-asset", "no-checksum").</summary>
        public static async Task<(UpdatePlan? Plan, string? Reason)> PlanAsync(HttpClient http, string currentVersion, CancellationToken ct = default)
        {
            var rel = await ReleaseDownloader.GetLatestAsync(http, Repo, ct);
            if (rel == null) return (null, "no-release");
            var version = rel.Tag.TrimStart('v', 'V');
            if (!VersionComparer.IsNewer(rel.Tag, currentVersion)) return (null, "up-to-date");

            var asset = rel.Assets.FirstOrDefault(a => a.Name == InstallerNameFor(version) && Uri.TryCreate(a.Url, UriKind.Absolute, out var u) && SuiteCatalog.IsAllowedHost(u));
            if (asset == null) return (null, "no-asset");

            var hash = await ReleaseDownloader.ResolveExpectedHashAsync(http, rel, asset, asset.Name + ".sha256", ct);
            if (hash == null) return (null, "no-checksum");
            return (new UpdatePlan(version, asset, hash, rel.HtmlUrl), null);
        }

        /// <summary>Μόνο ένα υπαρκτό, πλήρως ορισμένο <c>KombosSetup-x.y.z.exe</c> εκκινείται ποτέ.</summary>
        public static bool IsSafeSetupPath(string path) =>
            Path.IsPathFullyQualified(path) && SetupNameRx.IsMatch(Path.GetFileName(path)) && File.Exists(path);

        /// <summary>Κανονικός (όχι σιωπηλός) installer: UAC από το manifest του setup, wizard με «Εκκίνηση Κόμβου» στο τέλος, log, χωρίς επανεκκίνηση PC.</summary>
        public static ProcessStartInfo BuildStartInfo(string setupPath, string logPath)
        {
            if (!IsSafeSetupPath(setupPath)) throw new InvalidOperationException("not a Kombos setup file: " + setupPath);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            return new ProcessStartInfo(setupPath)
            {
                Arguments = $"/NORESTART /LOG=\"{logPath}\"",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(setupPath)!
            };
        }
    }
}
