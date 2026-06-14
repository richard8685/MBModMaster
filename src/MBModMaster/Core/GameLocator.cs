using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;

namespace MBModMaster.Core;

public sealed class GameLocator
{
    private const string SteamAppId = "261550";
    private const string GameFolderName = "Mount & Blade II Bannerlord";

    public string? LocateBannerlord()
    {
        foreach (var libraryPath in GetSteamLibraryPaths().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var manifestPath = Path.Combine(libraryPath, "steamapps", $"appmanifest_{SteamAppId}.acf");
            var gamePath = Path.Combine(libraryPath, "steamapps", "common", GameFolderName);

            if (File.Exists(manifestPath) && Directory.Exists(gamePath))
            {
                return gamePath;
            }
        }

        return GetFallbackGamePaths().FirstOrDefault(Directory.Exists);
    }

    private static IEnumerable<string> GetSteamLibraryPaths()
    {
        var steamPath = ReadSteamInstallPath();
        if (!string.IsNullOrWhiteSpace(steamPath))
        {
            yield return steamPath;

            var libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            foreach (var libraryPath in ReadLibraryFolders(libraryFile))
            {
                yield return libraryPath;
            }
        }

        foreach (var path in GetFallbackSteamRoots())
        {
            yield return path;
        }
    }

    private static string? ReadSteamInstallPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (key?.GetValue("SteamPath") is string steamPath)
        {
            return steamPath;
        }

        return key?.GetValue("SteamExe") is string exePath ? Path.GetDirectoryName(exePath) : null;
    }

    private static IEnumerable<string> ReadLibraryFolders(string libraryFile)
    {
        if (!File.Exists(libraryFile))
        {
            yield break;
        }

        var content = File.ReadAllText(libraryFile);
        foreach (Match match in Regex.Matches(content, "\"path\"\\s+\"(?<path>.+?)\""))
        {
            var path = match.Groups["path"].Value.Replace(@"\\", @"\");
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> GetFallbackSteamRoots()
    {
        yield return @"C:\Program Files (x86)\Steam";
        yield return @"D:\Steam";
        yield return @"D:\SteamLibrary";
        yield return @"E:\Steam";
        yield return @"E:\SteamLibrary";
    }

    private static IEnumerable<string> GetFallbackGamePaths()
    {
        foreach (var root in GetFallbackSteamRoots())
        {
            yield return Path.Combine(root, "steamapps", "common", GameFolderName);
            yield return Path.Combine(root, "common", GameFolderName);
        }
    }
}
