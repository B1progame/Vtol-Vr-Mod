using System;
using System.Collections.ObjectModel;
using System.Linq;
using Material.Icons;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed record ProfileIconOption(string DisplayName, string IconName, MaterialIconKind Kind);

public static class ProfileIconCatalog
{
    public const string DefaultIconName = nameof(MaterialIconKind.AccountGroup);

    public static ObservableCollection<ProfileIconOption> Options { get; } = new(new[]
    {
        Create("Squadron", MaterialIconKind.AccountGroup),
        Create("Air Patrol", MaterialIconKind.Airplane),
        Create("Takeoff", MaterialIconKind.AirplaneTakeoff),
        Create("Landing", MaterialIconKind.AirplaneLanding),
        Create("Air Marker", MaterialIconKind.AirplaneMarker),
        Create("Protected Flight", MaterialIconKind.ShieldAirplane),
        Create("Rocket Run", MaterialIconKind.RocketLaunch),
        Create("Helicopter", MaterialIconKind.Helicopter),
        Create("Target Practice", MaterialIconKind.Target),
        Create("Radar", MaterialIconKind.Radar),
        Create("Strike Package", MaterialIconKind.SwordCross),
        Create("Game Night", MaterialIconKind.GamepadVariant),
        Create("Favorite", MaterialIconKind.Star),
        Create("Archive", MaterialIconKind.FolderStar),
        Create("Maintenance", MaterialIconKind.Wrench),
        Create("Mod Pack", MaterialIconKind.PackageVariantClosed),
        Create("Server", MaterialIconKind.Server),
        Create("Race", MaterialIconKind.FlagCheckered),
        Create("Trophy", MaterialIconKind.Trophy),
        Create("Medal", MaterialIconKind.Medal),
        Create("Navigation", MaterialIconKind.Compass),
        Create("Map", MaterialIconKind.Map),
        Create("Carrier Ops", MaterialIconKind.ShipWheel),
        Create("Fast Mission", MaterialIconKind.LightningBolt),
        Create("Danger", MaterialIconKind.AlertCircle),
        Create("GPS", MaterialIconKind.CrosshairsGps),
        Create("Workload", MaterialIconKind.Briefcase),
        Create("Defense", MaterialIconKind.Shield)
    });

    public static ProfileIconOption GetOption(string? iconName)
    {
        var normalized = NormalizeIconName(iconName);
        return Options.FirstOrDefault(option => string.Equals(option.IconName, normalized, StringComparison.Ordinal))
               ?? Options[0];
    }

    public static MaterialIconKind GetKind(string? iconName) => GetOption(iconName).Kind;

    public static string NormalizeIconName(string? iconName) => Enum.TryParse<MaterialIconKind>(iconName, out var kind)
        ? kind.ToString()
        : DefaultIconName;

    private static ProfileIconOption Create(string displayName, MaterialIconKind kind) =>
        new(displayName, kind.ToString(), kind);
}
