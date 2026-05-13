using System.Collections.Generic;

namespace VTOLVRWorkshopProfileSwitcher.Models;

public sealed class ServerBrowserResult
{
    public bool IsSteamRunning { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public IReadOnlyList<VtolServerLobby> Servers { get; init; } = [];
}
