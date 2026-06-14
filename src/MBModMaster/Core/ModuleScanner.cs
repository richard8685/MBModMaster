using MBModMaster.Models;
using System.IO;
using System.Xml.Linq;

namespace MBModMaster.Core;

public sealed class ModuleScanner
{
    public IReadOnlyList<BannerlordModule> Scan(string gameDirectory)
    {
        var modulesDirectory = Path.Combine(gameDirectory, "Modules");
        if (!Directory.Exists(modulesDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(modulesDirectory)
            .Select(ReadModule)
            .OrderBy(module => GetBaseModuleOrder(module.Id))
            .ThenBy(module => module.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static BannerlordModule ReadModule(string moduleDirectory)
    {
        var directoryName = Path.GetFileName(moduleDirectory);
        var subModulePath = Path.Combine(moduleDirectory, "SubModule.xml");

        if (!File.Exists(subModulePath))
        {
            return CreateFallbackModule(directoryName, moduleDirectory);
        }

        try
        {
            var document = XDocument.Load(subModulePath, LoadOptions.PreserveWhitespace);
            var root = document.Root;

            return new BannerlordModule
            {
                Id = GetValue(root, "Id") ?? directoryName,
                Name = GetValue(root, "Name") ?? directoryName,
                Version = GetValue(root, "Version"),
                DirectoryName = directoryName,
                DirectoryPath = moduleDirectory,
                Dependencies = ReadDependencies(root),
                LoadAfterThisModuleIds = ReadLoadAfterThisModuleIds(root),
                IsSinglePlayer = IsSinglePlayerModule(root),
                IsMultiPlayer = IsMultiPlayerModule(root),
                HasSubModuleXml = true
            };
        }
        catch
        {
            return CreateFallbackModule(directoryName, moduleDirectory);
        }
    }

    private static BannerlordModule CreateFallbackModule(string directoryName, string moduleDirectory)
    {
        return new BannerlordModule
        {
            Id = directoryName,
            Name = directoryName,
            DirectoryName = directoryName,
            DirectoryPath = moduleDirectory,
            HasSubModuleXml = false
        };
    }

    private static string? GetValue(XElement? root, string elementName)
    {
        var element = root?.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, elementName, StringComparison.OrdinalIgnoreCase));
        return element?.Attribute("value")?.Value
            ?? element?.Attribute("Value")?.Value
            ?? element?.Value;
    }

    private static IReadOnlyList<string> ReadDependencies(XElement? root)
    {
        var moduleIds = new List<string>();
        var dependenciesElement = root?.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "DependedModules", StringComparison.OrdinalIgnoreCase));

        if (dependenciesElement is not null)
        {
            moduleIds.AddRange(dependenciesElement.Elements()
                .Where(e => string.Equals(e.Name.LocalName, "DependedModule", StringComparison.OrdinalIgnoreCase))
                .Select(ReadIdAttribute)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!));
        }

        moduleIds.AddRange(ReadDependencyMetadata(root, "LoadBeforeThis"));

        return moduleIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> ReadLoadAfterThisModuleIds(XElement? root)
    {
        var moduleIds = new List<string>();
        var modulesToLoadAfterThis = root?.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "ModulesToLoadAfterThis", StringComparison.OrdinalIgnoreCase));

        if (modulesToLoadAfterThis is not null)
        {
            moduleIds.AddRange(modulesToLoadAfterThis.Elements()
                .Where(e => string.Equals(e.Name.LocalName, "Module", StringComparison.OrdinalIgnoreCase))
                .Select(ReadIdAttribute)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!));
        }

        moduleIds.AddRange(ReadDependencyMetadata(root, "LoadAfterThis"));

        return moduleIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> ReadDependencyMetadata(XElement? root, string order)
    {
        var metadataElement = root?.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "DependedModuleMetadatas", StringComparison.OrdinalIgnoreCase));

        if (metadataElement is null)
        {
            return [];
        }

        return metadataElement.Elements()
            .Where(e => string.Equals(e.Name.LocalName, "DependedModuleMetadata", StringComparison.OrdinalIgnoreCase))
            .Where(e => string.Equals(e.Attribute("order")?.Value, order, StringComparison.OrdinalIgnoreCase))
            .Select(ReadIdAttribute)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ReadIdAttribute(XElement element)
    {
        return element.Attribute("Id")?.Value
            ?? element.Attribute("id")?.Value;
    }

    private static bool? ReadBool(XElement? root, string elementName)
    {
        var value = GetValue(root, elementName);
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool IsSinglePlayerModule(XElement? root)
    {
        var explicitValue = ReadBool(root, "SingleplayerModule") ?? ReadBool(root, "SinglePlayerModule");
        if (explicitValue is not null)
        {
            return explicitValue.Value;
        }

        var category = GetValue(root, "ModuleCategory");
        return category?.Contains("Singleplayer", StringComparison.OrdinalIgnoreCase) == true
            || category?.Contains("SinglePlayer", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsMultiPlayerModule(XElement? root)
    {
        var explicitValue = ReadBool(root, "MultiplayerModule") ?? ReadBool(root, "MultiPlayerModule");
        if (explicitValue is not null)
        {
            return explicitValue.Value;
        }

        var category = GetValue(root, "ModuleCategory");
        return category?.Contains("Multiplayer", StringComparison.OrdinalIgnoreCase) == true
            || category?.Contains("MultiPlayer", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static int GetBaseModuleOrder(string moduleId)
    {
        var index = Array.FindIndex(BannerlordModule.BaseModuleIds, id => string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? BannerlordModule.BaseModuleIds.Length : index;
    }
}
