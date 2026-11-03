using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Kombos.Localization;
using Xunit;

namespace Kombos.Tests
{
    /// <summary>Η έκδοση υπάρχει σε τρία σημεία (L.AppVersion, csproj &lt;Version&gt;, installer #define) - πρέπει να συμφωνούν,
    /// αλλιώς το installer-e2e (που διαβάζει το .exe) ή το "Οδηγός ανά έκδοση" δουλεύουν με λάθος αριθμό.</summary>
    public class VersionSyncTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Kombos.csproj"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }

        [Fact]
        public void AppVersion_MatchesAssemblyAndInstaller()
        {
            var infoVersion = typeof(L).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
            Assert.StartsWith(L.AppVersion, infoVersion);

            var iss = File.ReadAllText(Path.Combine(RepoRoot(), "installer", "Kombos.iss"));
            var m = Regex.Match(iss, "#define MyAppVersion \"([^\"]+)\"");
            Assert.True(m.Success);
            Assert.Equal(L.AppVersion, m.Groups[1].Value);
        }

        [Fact]
        public void InstallerIsMachineWide_AndUsesASingleBraceInPascalKey()
        {
            var iss = File.ReadAllText(Path.Combine(RepoRoot(), "installer", "Kombos.iss"));
            Assert.Contains("PrivilegesRequired=admin", iss);
            Assert.Contains("DefaultDirName={autopf}\\{#MyAppNameAscii}", iss);
            Assert.DoesNotContain("PrivilegesRequired=lowest", iss);
            // [Setup] AppId uses "{{", the Pascal string must use ONE brace (the Opsis News Crawler bug)
            var appId = Regex.Match(iss, @"AppId=\{\{([0-9A-Fa-f-]+)\}").Groups[1].Value;
            Assert.NotEmpty(appId);
            Assert.Contains("Uninstall\\{" + appId + "}_is1", iss);
            Assert.DoesNotContain("Uninstall\\{{", iss);
        }
    }
}
