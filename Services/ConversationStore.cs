using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Kombos.Models;

namespace Kombos.Services
{
    /// <summary>Αποθηκεύει τη λίστα συζητήσεων του χρήστη σε %AppData%\Kombos\conversations.json από
    /// προεπιλογή, ή σε έναν φάκελο της επιλογής του χρήστη (π.χ. OneDrive/Dropbox/δικτυακό δίσκο) - αυτό
    /// είναι το μοντέλο "συνεργασίας ανάμεσα σε υπολογιστές" του Κόμβου: μοιράσου το ΙΔΙΟ αρχείο
    /// conversations.json μέσω ενός φακέλου συγχρονισμένου εξωτερικά, χωρίς το Κόμβος να χρειάζεται δικό του
    /// server/backend. Δεν γίνεται merge ταυτόχρονων αλλαγών - το τελευταίο Save() που γράφεται κερδίζει,
    /// όπως ακριβώς και κάθε άλλο αρχείο μέσα σε έναν φάκελο συγχρονισμού cloud.</summary>
    public static class ConversationStore
    {
        private static readonly string DefaultFilePath = Path.Combine(
            AppPaths.Root, "conversations.json");

        private static string ResolveFilePath(string? customFolder) =>
            string.IsNullOrWhiteSpace(customFolder) ? DefaultFilePath : Path.Combine(customFolder, "conversations.json");

        public static List<Conversation> Load(string? customFolder = null)
        {
            var filePath = ResolveFilePath(customFolder);
            // το κύριο αρχείο, αλλιώς το προηγούμενο αντίγραφο (.bak, γράφεται ατομικά σε κάθε Save) - ποτέ άδεια λίστα αν υπάρχει υγιές αντίγραφο
            foreach (var candidate in new[] { filePath, filePath + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    var list = JsonSerializer.Deserialize<List<Conversation>>(File.ReadAllText(candidate));
                    if (list != null) return list;
                }
                catch { /* corrupt file - try the backup, then start with an empty list */ }
            }
            return new List<Conversation>();
        }

        /// <summary>Συμπαγές JSON (χωρίς indent: μικρότερο αρχείο, ταχύτερη σειριοποίηση).</summary>
        public static string Serialize(List<Conversation> conversations) => JsonSerializer.Serialize(conversations);

        /// <summary>Ατομική εγγραφή: πρώτα σε .tmp στον ίδιο φάκελο, μετά File.Replace (κρατά το προηγούμενο ως .bak) - αν το process
        /// πεθάνει στη μέση, το conversations.json μένει πάντα ολόκληρο (παλιό ή νέο), ποτέ μισογραμμένο.</summary>
        public static void WriteAtomic(string filePath, string json)
        {
            var dir = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(dir);
            var tmp = filePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(filePath)) File.Replace(tmp, filePath, filePath + ".bak", ignoreMetadataErrors: true);
            else File.Move(tmp, filePath);
        }

        public static void Save(List<Conversation> conversations, string? customFolder = null)
        {
            try { WriteAtomic(ResolveFilePath(customFolder), Serialize(conversations)); }
            catch { /* best-effort - not critical if it fails */ }
        }

        private static readonly System.Threading.SemaphoreSlim WriteLock = new(1, 1);

        /// <summary>Γράφει ένα ΗΔΗ σειριοποιημένο snapshot στο παρασκήνιο, μία εγγραφή τη φορά (οι εγγραφές δεν μπλέκονται ποτέ).</summary>
        public static async System.Threading.Tasks.Task SaveSnapshotAsync(string json, string? customFolder = null)
        {
            await WriteLock.WaitAsync().ConfigureAwait(false);
            try { await System.Threading.Tasks.Task.Run(() => { try { WriteAtomic(ResolveFilePath(customFolder), json); } catch { } }).ConfigureAwait(false); }
            finally { WriteLock.Release(); }
        }
    }
}
