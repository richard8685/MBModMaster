using MBModMaster.Core;
using MBModMaster.Models;
using System.IO.Compression;
using System.Xml.Linq;

var root = Path.Combine(Path.GetTempPath(), "MBModMasterSmokeTests", Guid.NewGuid().ToString("N"));
var configPath = Path.Combine(root, "Configs", "LauncherData.xml");

var service = new LauncherConfigService(configPath);
var modules = new List<BannerlordModule>
{
    new()
    {
        Id = "Native",
        Name = "Native",
        DirectoryName = "Native",
        DirectoryPath = "Native",
        IsSinglePlayer = true,
        HasSubModuleXml = true,
        IsEnabled = true,
        LoadOrder = 1
    },
    new()
    {
        Id = "ExampleMod",
        Name = "Example Mod",
        DirectoryName = "ExampleMod",
        DirectoryPath = "ExampleMod",
        IsSinglePlayer = true,
        HasSubModuleXml = true,
        IsEnabled = true,
        LoadOrder = 2
    },
    new()
    {
        Id = "MultiplayerOnly",
        Name = "Multiplayer Only",
        DirectoryName = "MultiplayerOnly",
        DirectoryPath = "MultiplayerOnly",
        IsMultiPlayer = true,
        HasSubModuleXml = true,
        IsEnabled = false,
        LoadOrder = 3
    }
};

var firstSave = service.SaveSingleplayerState(modules);
if (!File.Exists(firstSave.ConfigPath))
{
    throw new InvalidOperationException("Config file was not created.");
}

var snapshot = service.Read();
if (!snapshot.Exists || snapshot.Modules.Count != 2)
{
    throw new InvalidOperationException("Saved config did not round-trip expected singleplayer modules.");
}

var document = XDocument.Load(configPath);
var officialEntries = document.Root?
    .Element("SingleplayerData")?
    .Element("ModDatas")?
    .Elements("UserModData")
    .ToList();

if (officialEntries is null || officialEntries.Count != 2 || officialEntries.Any(entry => entry.Attribute("Id") is not null))
{
    throw new InvalidOperationException("Saved config does not use the official UserModData element structure.");
}

modules[1].IsEnabled = false;
var secondSave = service.SaveSingleplayerState(modules);
if (secondSave.BackupPath is null || !File.Exists(secondSave.BackupPath))
{
    throw new InvalidOperationException("Backup file was not created on second save.");
}

var loadOrderService = new LoadOrderService();
var sorted = loadOrderService.Sort([
    modules[1],
    new BannerlordModule
    {
        Id = "DependentMod",
        Name = "Dependent Mod",
        DirectoryName = "DependentMod",
        DirectoryPath = "DependentMod",
        IsSinglePlayer = true,
        HasSubModuleXml = true,
        IsEnabled = true,
        LoadOrder = 1,
        Dependencies = ["ExampleMod"]
    }
]);

if (sorted.Modules.Select(module => module.Id).ToArray() is not ["ExampleMod", "DependentMod"])
{
    throw new InvalidOperationException("Dependency sorting did not place dependencies first.");
}

var gameRoot = Path.Combine(root, "Game");
var modulesDirectory = Path.Combine(gameRoot, "Modules");
Directory.CreateDirectory(modulesDirectory);

var archiveSource = Path.Combine(root, "ArchiveSource", "ExampleImported");
Directory.CreateDirectory(archiveSource);
File.WriteAllText(Path.Combine(archiveSource, "SubModule.xml"), "<Module><Name value=\"Example Imported\"/><Id value=\"ExampleImported\"/><ModuleCategory value=\"Singleplayer\"/></Module>");
File.WriteAllText(Path.Combine(archiveSource, "ExampleImported.dll"), "placeholder");

var archivePath = Path.Combine(root, "ExampleImported.zip");
ZipFile.CreateFromDirectory(Path.Combine(root, "ArchiveSource"), archivePath);

var installer = new ArchiveInstaller();
var installResult = installer.Install(archivePath, gameRoot);
if (installResult.InstalledModules.Count != 1 || !File.Exists(Path.Combine(modulesDirectory, "ExampleImported", "SubModule.xml")))
{
    throw new InvalidOperationException("Archive import did not install the module.");
}

var secondInstallResult = installer.Install(archivePath, gameRoot);
if (secondInstallResult.InstalledModules[0].BackupPath is null || !Directory.Exists(secondInstallResult.InstalledModules[0].BackupPath))
{
    throw new InvalidOperationException("Archive import did not back up an existing module.");
}

var scannedModules = new ModuleScanner().Scan(gameRoot);
var importedModule = scannedModules.Single(module => module.Id == "ExampleImported");
if (!importedModule.IsSinglePlayerCompatible)
{
    throw new InvalidOperationException("ModuleCategory=Singleplayer was not recognized as singleplayer compatible.");
}

var modulesArgument = GameLauncher.BuildModulesArgument(["Native", "Sandbox", "ExampleMod"]);
if (modulesArgument != "_MODULES_*Native*Sandbox*ExampleMod*_MODULES_")
{
    throw new InvalidOperationException("Direct launch module argument format is invalid.");
}

var foundationSorted = loadOrderService.Sort([
    new BannerlordModule
    {
        Id = "Native",
        Name = "Native",
        DirectoryName = "Native",
        DirectoryPath = "Native",
        IsSinglePlayer = true,
        HasSubModuleXml = true,
        IsEnabled = true,
        LoadOrder = 1
    },
    new BannerlordModule
    {
        Id = "Bannerlord.ButterLib",
        Name = "ButterLib",
        DirectoryName = "Bannerlord.ButterLib",
        DirectoryPath = "Bannerlord.ButterLib",
        IsSinglePlayer = true,
        HasSubModuleXml = true,
        IsEnabled = true,
        LoadOrder = 2,
        Dependencies = ["Bannerlord.Harmony"]
    },
    new BannerlordModule
    {
        Id = "Bannerlord.Harmony",
        Name = "Harmony",
        DirectoryName = "Bannerlord.Harmony",
        DirectoryPath = "Bannerlord.Harmony",
        IsSinglePlayer = true,
        HasSubModuleXml = true,
        IsEnabled = true,
        LoadOrder = 3
    }
]);

if (foundationSorted.Modules.Select(module => module.Id).ToArray() is not ["Bannerlord.Harmony", "Bannerlord.ButterLib", "Native"])
{
    throw new InvalidOperationException("Foundation modules must be sorted before native modules.");
}

var butterLibMetadataSorted = loadOrderService.Sort([
    new BannerlordModule
    {
        Id = "Native",
        Name = "Native",
        DirectoryName = "Native",
        DirectoryPath = "Native",
        IsSinglePlayer = true,
        HasSubModuleXml = true,
        IsEnabled = true,
        LoadOrder = 1
    },
    new BannerlordModule
    {
        Id = "Bannerlord.ButterLib",
        Name = "ButterLib",
        DirectoryName = "Bannerlord.ButterLib",
        DirectoryPath = "Bannerlord.ButterLib",
        IsSinglePlayer = true,
        HasSubModuleXml = true,
        IsEnabled = true,
        LoadOrder = 2,
        Dependencies = ["Bannerlord.Harmony", "BetterExceptionWindow"],
        LoadAfterThisModuleIds = ["Native"]
    },
    new BannerlordModule
    {
        Id = "BetterExceptionWindow",
        Name = "BetterExceptionWindow",
        DirectoryName = "Bannerlord.BetterExceptionWindow",
        DirectoryPath = "Bannerlord.BetterExceptionWindow",
        IsSinglePlayer = true,
        HasSubModuleXml = true,
        IsEnabled = true,
        LoadOrder = 3,
        Dependencies = ["Bannerlord.Harmony"]
    },
    new BannerlordModule
    {
        Id = "Bannerlord.Harmony",
        Name = "Harmony",
        DirectoryName = "Bannerlord.Harmony",
        DirectoryPath = "Bannerlord.Harmony",
        IsSinglePlayer = true,
        HasSubModuleXml = true,
        IsEnabled = true,
        LoadOrder = 4
    }
]);

if (butterLibMetadataSorted.Modules.Select(module => module.Id).ToArray() is not ["Bannerlord.Harmony", "BetterExceptionWindow", "Bannerlord.ButterLib", "Native"])
{
    throw new InvalidOperationException("LoadBeforeThis/LoadAfterThis metadata was not respected.");
}

Console.WriteLine($"OK {configPath}");
