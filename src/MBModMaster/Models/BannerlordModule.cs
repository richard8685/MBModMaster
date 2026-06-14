namespace MBModMaster.Models;

public sealed class BannerlordModule
{
    public static readonly string[] BaseModuleIds = ["Native", "SandBoxCore", "CustomBattle", "Sandbox", "StoryMode", "BirthAndDeath"];
    public static readonly string[] FoundationModuleIds =
    [
        "Bannerlord.Harmony",
        "Bannerlord.ButterLib",
        "Bannerlord.UIExtenderEx",
        "Bannerlord.MBOptionScreen"
    ];

    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Version { get; init; }
    public required string DirectoryName { get; init; }
    public required string DirectoryPath { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public IReadOnlyList<string> LoadAfterThisModuleIds { get; init; } = [];
    public bool IsEnabled { get; set; }
    public int? LoadOrder { get; set; }
    public bool IsSinglePlayer { get; init; }
    public bool IsMultiPlayer { get; init; }
    public bool HasSubModuleXml { get; init; }
    public bool IsBaseModule => BaseModuleIds.Any(id => string.Equals(id, Id, StringComparison.OrdinalIgnoreCase));
    public bool IsSinglePlayerCompatible => IsBaseModule || IsSinglePlayer;
    public bool CanToggle => HasSubModuleXml && IsSinglePlayerCompatible && !IsBaseModule;

    public string DependenciesText => Dependencies.Count == 0 ? "-" : string.Join(", ", Dependencies);
    public string VersionText => string.IsNullOrWhiteSpace(Version) ? "-" : Version;
    public string LoadOrderText => LoadOrder is null ? "-" : LoadOrder.Value.ToString();
    public string DisplayTagText
    {
        get
        {
            if (IsBaseModule)
            {
                return "官方";
            }

            if (IsFoundationModule)
            {
                return "前置";
            }

            return string.Empty;
        }
    }

    public bool IsFoundationModule
    {
        get
        {
            return FoundationModuleIds.Any(id => string.Equals(id, Id, StringComparison.OrdinalIgnoreCase));
        }
    }

    public string CategoryText => IsBaseModule
        ? "Base"
        : IsSinglePlayerCompatible ? "Singleplayer" : "MP only";

    public string GameModeText => (IsSinglePlayer, IsMultiPlayer) switch
    {
        (true, true) => "SP / MP",
        (true, false) => "SP",
        (false, true) => "MP",
        _ => "-"
    };

    public string StatusText
    {
        get
        {
            if (!HasSubModuleXml)
            {
                return "Missing SubModule.xml";
            }

            if (IsBaseModule)
            {
                return "Required";
            }

            return IsSinglePlayerCompatible ? "OK" : "MP only";
        }
    }
}
