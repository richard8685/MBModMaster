using System.IO;
using System.Xml.Linq;
using MBModMaster.Models;

namespace MBModMaster.Core;

public sealed class LauncherConfigService
{
    private static readonly string[] EnabledAttributeNames =
    [
        "IsSelected",
        "isSelected",
        "IsEnabled",
        "isEnabled",
        "Enabled",
        "enabled"
    ];

    public LauncherConfigService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "Configs",
            "LauncherData.xml"))
    {
    }

    public LauncherConfigService(string configPath)
    {
        ConfigPath = configPath;
    }

    public string ConfigPath { get; }

    public LauncherConfigSnapshot Read()
    {
        if (!File.Exists(ConfigPath))
        {
            return LauncherConfigSnapshot.Empty(ConfigPath);
        }

        try
        {
            var document = XDocument.Load(ConfigPath);
            var entries = document.Descendants()
                .Select((element, index) => new { Element = element, Index = index })
                .Select(item => ReadEntry(item.Element, item.Index))
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .GroupBy(entry => entry.ModuleId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            return new LauncherConfigSnapshot(ConfigPath, true, entries);
        }
        catch
        {
            return new LauncherConfigSnapshot(ConfigPath, false, []);
        }
    }

    public SaveLauncherConfigResult SaveSingleplayerState(IReadOnlyList<BannerlordModule> modules)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

        var backupPath = BackupExistingConfig();
        var document = File.Exists(ConfigPath)
            ? XDocument.Load(ConfigPath)
            : CreateDefaultDocument();

        var container = FindModuleContainer(document) ?? CreateSingleplayerContainer(document);
        ReplaceModuleEntries(container, modules);

        document.Save(ConfigPath);
        return new SaveLauncherConfigResult(ConfigPath, backupPath, modules.Count(module => module.IsEnabled));
    }

    private static LauncherModuleEntry? ReadEntry(XElement element, int loadOrder)
    {
        var moduleId = ReadId(element);
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return null;
        }

        var enabledValue = ReadEnabled(element);
        if (enabledValue is null)
        {
            return null;
        }

        return new LauncherModuleEntry(moduleId, enabledValue.Value, loadOrder);
    }

    private static string? ReadId(XElement element)
    {
        return element.Attribute("Id")?.Value
            ?? element.Attribute("id")?.Value
            ?? element.Attribute("ModuleId")?.Value
            ?? element.Attribute("moduleId")?.Value
            ?? element.Element("Id")?.Attribute("value")?.Value
            ?? element.Element("Id")?.Value;
    }

    private static bool? ReadEnabled(XElement element)
    {
        foreach (var attributeName in EnabledAttributeNames)
        {
            var value = element.Attribute(attributeName)?.Value;
            if (TryReadBool(value, out var enabled))
            {
                return enabled;
            }
        }

        foreach (var childName in EnabledAttributeNames)
        {
            var child = element.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, childName, StringComparison.OrdinalIgnoreCase));
            var value = child?.Attribute("value")?.Value ?? child?.Value;
            if (TryReadBool(value, out var enabled))
            {
                return enabled;
            }
        }

        return null;
    }

    private static bool TryReadBool(string? value, out bool enabled)
    {
        enabled = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out enabled))
        {
            return true;
        }

        if (value == "1")
        {
            enabled = true;
            return true;
        }

        if (value == "0")
        {
            enabled = false;
            return true;
        }

        return false;
    }

    private string? BackupExistingConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            return null;
        }

        var backupDirectory = Path.Combine(Path.GetDirectoryName(ConfigPath)!, "Backups");
        Directory.CreateDirectory(backupDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupPath = Path.Combine(backupDirectory, $"LauncherData.{timestamp}.xml");
        File.Copy(ConfigPath, backupPath, overwrite: false);
        return backupPath;
    }

    private static XDocument CreateDefaultDocument()
    {
        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("UserData",
                new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XElement("GameType", "Singleplayer"),
                new XElement("SingleplayerData",
                    new XElement("ModDatas")),
                new XElement("MultiplayerData",
                    new XElement("ModDatas")),
                new XElement("DLLCheckData",
                    new XElement("DLLData"))));
    }

    private static XElement? FindModuleContainer(XDocument document)
    {
        var officialContainer = document.Root?
            .Element("SingleplayerData")?
            .Element("ModDatas");

        if (officialContainer is not null)
        {
            return officialContainer;
        }

        return document.Descendants()
            .Select(element => new
            {
                Element = element,
                DirectModuleCount = element.Elements().Count(child => ReadId(child) is not null),
                SingleplayerScore = HasSingleplayerContext(element) ? 1000 : 0
            })
            .Where(candidate => candidate.DirectModuleCount > 0)
            .OrderByDescending(candidate => candidate.SingleplayerScore + candidate.DirectModuleCount)
            .Select(candidate => candidate.Element)
            .FirstOrDefault();
    }

    private static bool HasSingleplayerContext(XElement element)
    {
        return element.AncestorsAndSelf()
            .Any(ancestor => ancestor.Name.LocalName.Contains("Singleplayer", StringComparison.OrdinalIgnoreCase)
                || ancestor.Name.LocalName.Contains("SinglePlayer", StringComparison.OrdinalIgnoreCase));
    }

    private static XElement CreateSingleplayerContainer(XDocument document)
    {
        if (document.Root is null)
        {
            document.Add(new XElement("UserData"));
        }

        var gameType = document.Root!.Element("GameType");
        if (gameType is null)
        {
            document.Root.AddFirst(new XElement("GameType", "Singleplayer"));
        }
        else
        {
            gameType.Value = "Singleplayer";
        }

        var singleplayerData = document.Root!.Elements()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "SingleplayerData", StringComparison.OrdinalIgnoreCase));

        if (singleplayerData is null)
        {
            singleplayerData = new XElement("SingleplayerData");
            document.Root.Add(singleplayerData);
        }

        var modDatas = singleplayerData.Elements()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "ModDatas", StringComparison.OrdinalIgnoreCase));

        if (modDatas is null)
        {
            modDatas = new XElement("ModDatas");
            singleplayerData.Add(modDatas);
        }

        return modDatas;
    }

    private static void ReplaceModuleEntries(XElement container, IReadOnlyList<BannerlordModule> modules)
    {
        var existingVersions = container.Elements()
            .Where(child => ReadId(child) is not null)
            .Select(child => new
            {
                Id = ReadId(child)!,
                Version = child.Element("LastKnownVersion")?.Value
            })
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Version, StringComparer.OrdinalIgnoreCase);

        foreach (var child in container.Elements().Where(child => ReadId(child) is not null).ToList())
        {
            child.Remove();
        }

        foreach (var module in modules
            .Where(module => module.IsSinglePlayerCompatible)
            .OrderBy(module => module.LoadOrder ?? int.MaxValue))
        {
            var version = !string.IsNullOrWhiteSpace(module.Version)
                ? module.Version
                : existingVersions.GetValueOrDefault(module.Id) ?? string.Empty;

            container.Add(new XElement("UserModData",
                new XElement("Id", module.Id),
                new XElement("LastKnownVersion", version),
                new XElement("IsSelected", module.IsEnabled.ToString().ToLowerInvariant())));
        }
    }
}

public sealed record LauncherConfigSnapshot(
    string ConfigPath,
    bool Exists,
    IReadOnlyList<LauncherModuleEntry> Modules)
{
    public static LauncherConfigSnapshot Empty(string configPath) => new(configPath, false, []);
}

public sealed record LauncherModuleEntry(string ModuleId, bool IsEnabled, int LoadOrder);

public sealed record SaveLauncherConfigResult(string ConfigPath, string? BackupPath, int EnabledCount);
