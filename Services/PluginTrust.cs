using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kombos.Services
{
    public enum PluginTrustState { Trusted, Unknown, Changed }

    public record PluginApproval(string Folder, PluginTrustState State, string Hash, IReadOnlyList<string> Files);

    /// <summary>Λίστα εγκεκριμένων plugins: φάκελος → SHA-256 ΟΛΩΝ των .dll του φακέλου. Αποθηκεύεται στο %AppData%\Kombos.</summary>
    public class PluginAllowListStore
    {
        private readonly string _path;
        public PluginAllowListStore(string? path = null) => _path = path ?? Path.Combine(AppPaths.Root, "plugin-allowlist.json");

        public Dictionary<string, string> Load()
        {
            try
            {
                if (File.Exists(_path))
                    return new Dictionary<string, string>(JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path)) ?? new(), StringComparer.OrdinalIgnoreCase);
            }
            catch { }
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public void Set(string folder, string hash)
        {
            try
            {
                var all = Load();
                all[folder] = hash;
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(all));
            }
            catch { }
        }
    }

    /// <summary>Μοντέλο εμπιστοσύνης plugins: ο φάκελος %AppData%\Kombos\plugins είναι εγγράψιμος από τον χρήστη (και από κάθε
    /// πρόγραμμα που τρέχει ως αυτός), άρα ένα .dll εκεί ΔΕΝ φορτώνεται αν δεν είναι εγκεκριμένο: (α) ο φάκελος είναι ίδιος
    /// (hash όλων των .dll) με το αντίστοιχο bundled plugin του φακέλου εγκατάστασης (Program Files), ή (β) το hash του έχει
    /// εγκριθεί ρητά από τον χρήστη. Αλλαγμένο περιεχόμενο ζητά νέα έγκριση.</summary>
    public static class PluginTrust
    {
        public static IReadOnlyList<string> DllFiles(string folder) =>
            Directory.Exists(folder) ? Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList() : new List<string>();

        /// <summary>SHA-256 πάνω σε "όνομα:hash" όλων των .dll του φακέλου (ταξινομημένα)· κενός φάκελος → "".</summary>
        public static string FolderHash(string folder)
        {
            var files = DllFiles(folder);
            if (files.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var f in files)
            {
                using var fs = File.OpenRead(f);
                sb.Append(Path.GetFileName(f).ToLowerInvariant()).Append(':').Append(Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant()).Append('\n');
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
        }

        public static PluginApproval Evaluate(string pluginFolderPath, IReadOnlyDictionary<string, string> allowList, string? bundledRoot)
        {
            var name = Path.GetFileName(pluginFolderPath);
            var hash = FolderHash(pluginFolderPath);
            var files = DllFiles(pluginFolderPath).Select(Path.GetFileName).Where(n => n != null).Select(n => n!).ToList();
            if (hash.Length > 0 && !string.IsNullOrEmpty(bundledRoot))
            {
                var bundled = FolderHash(Path.Combine(bundledRoot, name));
                if (bundled.Length > 0 && string.Equals(bundled, hash, StringComparison.Ordinal))
                    return new PluginApproval(name, PluginTrustState.Trusted, hash, files);
            }
            if (allowList.TryGetValue(name, out var approved))
                return new PluginApproval(name, string.Equals(approved, hash, StringComparison.OrdinalIgnoreCase) ? PluginTrustState.Trusted : PluginTrustState.Changed, hash, files);
            return new PluginApproval(name, PluginTrustState.Unknown, hash, files);
        }
    }
}
