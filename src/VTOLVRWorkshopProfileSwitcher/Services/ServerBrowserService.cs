using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using VTOLVRWorkshopProfileSwitcher.Models;

namespace VTOLVRWorkshopProfileSwitcher.Services;

public sealed class ServerBrowserService
{
    private const uint VtolVrAppId = 667970;
    private readonly SteamWorkshopInfoService _workshopInfoService = new();

    public async Task<ServerBrowserResult> LoadServersAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSteamRunning())
        {
            return new ServerBrowserResult
            {
                IsSteamRunning = false,
                StatusMessage = "Start Steam to view servers.",
                Servers = []
            };
        }

        var servers = new List<VtolServerLobby>();

        try
        {
            SteamClient.Init(VtolVrAppId, true);

            var lobbies = await SteamMatchmaking.LobbyList
                .FilterDistanceWorldwide()
                .WithSlotsAvailable(1)
                .WithMaxResults(250)
                .RequestAsync();

            if (lobbies is null)
            {
                return new ServerBrowserResult
                {
                    IsSteamRunning = true,
                    StatusMessage = "Steam is running, but the VTOL lobby query returned nothing.",
                    Servers = []
                };
            }

            foreach (var lobby in lobbies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lobby.Refresh();
                await WaitForMetadataAsync(lobby, cancellationToken);

                var server = ParseLobby(lobby);
                if (server is not null)
                {
                    servers.Add(server);
                }
            }

            await EnrichWorkshopDataAsync(servers, cancellationToken);

            var ordered = servers
                .OrderByDescending(server => server.IsModded)
                .ThenByDescending(server => server.CurrentPlayers)
                .ThenBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ServerBrowserResult
            {
                IsSteamRunning = true,
                StatusMessage = ordered.Count == 0
                    ? "No public joinable servers were found right now."
                    : $"Found {ordered.Count} public joinable server(s).",
                Servers = ordered
            };
        }
        catch (Exception ex)
        {
            return new ServerBrowserResult
            {
                IsSteamRunning = true,
                StatusMessage = $"Server refresh failed: {ex.Message}",
                Servers = []
            };
        }
        finally
        {
            if (SteamClient.IsValid)
            {
                SteamClient.Shutdown();
            }
        }
    }

    private static bool IsSteamRunning()
    {
        try
        {
            return Process.GetProcessesByName("steam").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task WaitForMetadataAsync(Lobby lobby, CancellationToken cancellationToken)
    {
        if (lobby.Data.Any())
        {
            return;
        }

        var started = DateTime.UtcNow;
        while ((DateTime.UtcNow - started).TotalMilliseconds < 150)
        {
            SteamClient.RunCallbacks();
            if (lobby.Data.Any())
            {
                return;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    private static VtolServerLobby? ParseLobby(Lobby lobby)
    {
        var data = lobby.Data
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        if (data.Count == 0)
        {
            return null;
        }

        var scenarioName = GetValue(data, "scn", "Unknown Scenario");
        var scenarioRawId = GetValue(data, "scID", string.Empty);
        var scenarioSource = "Unknown";
        var scenarioWorkshopId = string.Empty;
        var scenarioSubtitle = string.Empty;

        if (scenarioRawId.StartsWith("w,", StringComparison.OrdinalIgnoreCase))
        {
            scenarioSource = "Workshop";
            var parts = scenarioRawId.Split(',', 3, StringSplitOptions.None);
            if (parts.Length == 3)
            {
                scenarioSubtitle = parts[1];
                scenarioWorkshopId = parts[2];
            }
        }
        else if (scenarioRawId.StartsWith("b,", StringComparison.OrdinalIgnoreCase))
        {
            scenarioSource = "Built-in";
            scenarioSubtitle = scenarioRawId;
        }

        var requirements = ParseServerRequirements(data);
        var version = GetValue(data, "ver", "unknown");

        return new VtolServerLobby
        {
            LobbyId = lobby.Id.Value,
            Name = GetValue(data, "lName", "Unnamed Server"),
            CreatorName = GetValue(data, "oName", "Unknown"),
            CreatorSteamId = GetValue(data, "oId", string.Empty),
            State = GetValue(data, "gState", "Unknown"),
            GameVersion = version,
            JoinCode = GetValue(data, "plc", string.Empty),
            ScenarioName = scenarioName,
            ScenarioSource = scenarioSource,
            ScenarioWorkshopId = scenarioWorkshopId,
            ScenarioSubtitle = scenarioSubtitle,
            CurrentPlayers = lobby.MemberCount,
            MaxPlayers = lobby.MaxMembers,
            IsPasswordProtected = string.Equals(GetValue(data, "pwh", "0"), "1", StringComparison.Ordinal),
            IsDedicated = string.Equals(GetValue(data, "hs", "0"), "1", StringComparison.Ordinal) ||
                          data.ContainsKey("HCAutoJoinKey"),
            IsModded = requirements.Count > 0 || version.IndexOf('m') >= 0 || version.IndexOf('M') >= 0,
            Requirements = requirements,
            RequiredDlcIds = ParseDlcRequirements(GetValue(data, "dlcReq", string.Empty)),
            Metadata = data
        };
    }

    private async Task EnrichWorkshopDataAsync(List<VtolServerLobby> servers, CancellationToken cancellationToken)
    {
        var workshopIds = servers
            .SelectMany(server => server.Requirements.Select(requirement => requirement.WorkshopId))
            .Concat(servers
                .Select(server => server.ScenarioWorkshopId)
                .Where(id => !string.IsNullOrWhiteSpace(id)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (workshopIds.Count == 0)
        {
            return;
        }

        var info = await _workshopInfoService.GetItemInfoAsync(workshopIds, cancellationToken);
        foreach (var server in servers)
        {
            if (!string.IsNullOrWhiteSpace(server.ScenarioWorkshopId) &&
                info.TryGetValue(server.ScenarioWorkshopId, out var scenarioInfo))
            {
                server.ThumbnailPath = scenarioInfo.ThumbnailPath;
                if (string.IsNullOrWhiteSpace(server.ScenarioName) || server.ScenarioName == "Unknown Scenario")
                {
                    server.ScenarioName = scenarioInfo.Title;
                }
            }

            foreach (var requirement in server.Requirements)
            {
                if (!info.TryGetValue(requirement.WorkshopId, out var requirementInfo))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(requirement.Title) || requirement.Title == "Unknown Item")
                {
                    requirement.Title = requirementInfo.Title;
                }

                requirement.ThumbnailPath = requirementInfo.ThumbnailPath;
                server.ThumbnailPath ??= requirementInfo.ThumbnailPath;
            }
        }
    }

    private static List<ServerRequirement> ParseServerRequirements(Dictionary<string, string> data)
    {
        if (!data.TryGetValue("serverItems", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var requirements = new List<ServerRequirement>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var workshopId = item.TryGetProperty("SteamId", out var idEl)
                    ? idEl.ToString()
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(workshopId))
                {
                    continue;
                }

                var title = item.TryGetProperty("Title", out var titleEl)
                    ? titleEl.GetString() ?? $"Workshop Item {workshopId}"
                    : $"Workshop Item {workshopId}";

                requirements.Add(new ServerRequirement
                {
                    WorkshopId = workshopId,
                    Title = title,
                    Subtitle = workshopId
                });
            }

            return requirements;
        }
        catch
        {
            return [];
        }
    }

    private static List<string> ParseDlcRequirements(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string GetValue(Dictionary<string, string> data, string key, string fallback)
    {
        return data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }
}
