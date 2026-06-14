using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace MBModMaster.Core;

public sealed class ArchiveInstaller
{
    public InstallArchiveResult Install(string archivePath, string gameDirectory)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Archive file does not exist.", archivePath);
        }

        var modulesDirectory = Path.Combine(gameDirectory, "Modules");
        if (!Directory.Exists(modulesDirectory))
        {
            throw new DirectoryNotFoundException($"Modules folder was not found: {modulesDirectory}");
        }

        var extractionDirectory = Path.Combine(Path.GetTempPath(), "MBModMasterImports", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractionDirectory);

        try
        {
            ExtractArchive(archivePath, extractionDirectory);
            var moduleRoots = FindModuleRoots(extractionDirectory);
            if (moduleRoots.Count == 0)
            {
                throw new InvalidOperationException("No SubModule.xml was found in the archive.");
            }

            var installedModules = new List<InstalledModuleInfo>();
            foreach (var moduleRoot in moduleRoots)
            {
                var moduleName = Path.GetFileName(moduleRoot);
                var targetPath = Path.Combine(modulesDirectory, moduleName);
                var backupPath = BackupExistingModule(targetPath, modulesDirectory);

                CopyDirectory(moduleRoot, targetPath);
                var unblockedDllCount = UnblockDlls(targetPath);
                installedModules.Add(new InstalledModuleInfo(moduleName, targetPath, backupPath, unblockedDllCount));
            }

            return new InstallArchiveResult(installedModules);
        }
        finally
        {
            Directory.Delete(extractionDirectory, recursive: true);
        }
    }

    private static void ExtractArchive(string archivePath, string extractionDirectory)
    {
        var extension = Path.GetExtension(archivePath).ToLowerInvariant();
        if (extension == ".zip")
        {
            ZipFile.ExtractToDirectory(archivePath, extractionDirectory);
            return;
        }

        if (extension is ".7z" or ".rar")
        {
            ExtractWithSevenZip(archivePath, extractionDirectory);
            return;
        }

        throw new NotSupportedException("Supported archive formats: .zip, .7z, .rar");
    }

    private static void ExtractWithSevenZip(string archivePath, string extractionDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "7z",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("x");
        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add($"-o{extractionDirectory}");
        startInfo.ArgumentList.Add("-y");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start 7-Zip.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"7-Zip extraction failed. {stderr} {stdout}".Trim());
        }
    }

    private static IReadOnlyList<string> FindModuleRoots(string extractionDirectory)
    {
        var subModulePaths = Directory.EnumerateFiles(extractionDirectory, "SubModule.xml", SearchOption.AllDirectories)
            .ToList();

        return subModulePaths
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Where(path => !HasAncestorWithSubModule(path, subModulePaths))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasAncestorWithSubModule(string moduleRoot, IReadOnlyList<string> subModulePaths)
    {
        var parent = Directory.GetParent(moduleRoot);
        while (parent is not null)
        {
            var parentSubModule = Path.Combine(parent.FullName, "SubModule.xml");
            if (subModulePaths.Contains(parentSubModule, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            parent = parent.Parent;
        }

        return false;
    }

    private static string? BackupExistingModule(string targetPath, string modulesDirectory)
    {
        if (!Directory.Exists(targetPath))
        {
            return null;
        }

        var backupDirectory = Path.Combine(modulesDirectory, "_Backups");
        Directory.CreateDirectory(backupDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupPath = Path.Combine(backupDirectory, $"{Path.GetFileName(targetPath)}.{timestamp}");
        Directory.Move(targetPath, backupPath);
        return backupPath;
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            File.Copy(file, Path.Combine(targetDirectory, relativePath), overwrite: true);
        }
    }

    private static int UnblockDlls(string moduleDirectory)
    {
        var count = 0;
        foreach (var dllPath in Directory.EnumerateFiles(moduleDirectory, "*.dll", SearchOption.AllDirectories))
        {
            var zoneIdentifierPath = $"{dllPath}:Zone.Identifier";
            try
            {
                if (File.Exists(zoneIdentifierPath))
                {
                    File.Delete(zoneIdentifierPath);
                    count++;
                }
            }
            catch
            {
                // Missing permission on alternate data streams should not block installation.
            }
        }

        return count;
    }
}

public sealed record InstallArchiveResult(IReadOnlyList<InstalledModuleInfo> InstalledModules);

public sealed record InstalledModuleInfo(
    string ModuleName,
    string TargetPath,
    string? BackupPath,
    int UnblockedDllCount);
