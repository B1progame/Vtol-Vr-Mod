using System;
using System.Collections.Generic;

namespace VTOLVRWorkshopProfileSwitcher.Models;

public sealed class VtolServerLobby
{
    public required ulong LobbyId { get; set; }
    public string Name { get; set; } = "Unnamed Server";
    public string CreatorName { get; set; } = "Unknown";
    public string CreatorSteamId { get; set; } = string.Empty;
    public string State { get; set; } = "Unknown";
    public string GameVersion { get; set; } = "unknown";
    public string JoinCode { get; set; } = string.Empty;
    public string ScenarioName { get; set; } = "Unknown Scenario";
    public string ScenarioSource { get; set; } = "Unknown";
    public string ScenarioWorkshopId { get; set; } = string.Empty;
    public string ScenarioSubtitle { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public bool IsPasswordProtected { get; set; }
    public bool IsDedicated { get; set; }
    public bool IsModded { get; set; }
    public string PingText { get; set; } = "n/a";
    public List<ServerRequirement> Requirements { get; set; } = [];
    public List<string> MissingIds { get; set; } = [];
    public List<string> RequiredDlcIds { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
