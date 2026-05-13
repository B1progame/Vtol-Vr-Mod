using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media.Imaging;
using VTOLVRWorkshopProfileSwitcher.Models;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed class ServerItemViewModel
{
    public VtolServerLobby Source { get; }
    public string ServerName => Source.Name;
    public string CreatorName => Source.CreatorName;
    public string ScenarioName => Source.ScenarioName;
    public string CreatorLine => $"by {Source.CreatorName}";
    public string CardSubtitle => string.IsNullOrWhiteSpace(Source.ScenarioName)
        ? CreatorLine
        : $"{CreatorLine} - {Source.ScenarioName}";
    public string StatusText => $"{Source.CurrentPlayers}/{Source.MaxPlayers} - {(Source.IsModded ? "Modded" : "Vanilla")} - {PingText}";
    public string PlayerSummary => $"{Source.CurrentPlayers}/{Source.MaxPlayers}";
    public string ServerTypeSummary => Source.IsModded ? "Modded" : "Vanilla";
    public string PasswordSummary => Source.IsPasswordProtected ? "Password" : "Open";
    public string JoinCode => string.IsNullOrWhiteSpace(Source.JoinCode) ? "n/a" : Source.JoinCode;
    public string State => Source.State;
    public string GameVersion => Source.GameVersion;
    public string PingText => string.IsNullOrWhiteSpace(Source.PingText) || Source.PingText == "n/a"
        ? "Ping not exposed"
        : Source.PingText;
    public bool HasRequirements => RequiredItems.Count > 0;
    public int MissingCount => MissingItems.Count;
    public string MissingSummary => MissingCount == 0 ? "Ready" : $"{MissingCount} missing";
    public string DlcSummary => Source.RequiredDlcIds.Count == 0
        ? "None"
        : string.Join(", ", Source.RequiredDlcIds);
    public bool IsPasswordProtected => Source.IsPasswordProtected;
    public bool IsModded => Source.IsModded;
    public bool HasScenarioRequirement => ScenarioRequirement is not null;
    public string ScenarioSource => Source.ScenarioSource;
    public Bitmap? ThumbnailImage { get; }
    public List<ServerRequirementViewModel> RequiredItems { get; }
    public ServerRequirementViewModel? ScenarioRequirement { get; }
    public List<ServerRequirementViewModel> MissingItems { get; }

    public ServerItemViewModel(
        VtolServerLobby source,
        IReadOnlySet<string> installedModIds,
        IReadOnlySet<string> installedScenarioIds)
    {
        Source = source;
        ThumbnailImage = ViewModelImageLoader.TryLoadBitmap(source.ThumbnailPath);

        foreach (var requirement in Source.Requirements)
        {
            requirement.IsInstalled = installedModIds.Contains(requirement.WorkshopId);
            if (string.IsNullOrWhiteSpace(requirement.Subtitle))
            {
                requirement.Subtitle = requirement.WorkshopId;
            }
        }

        RequiredItems = Source.Requirements
            .Select(requirement => new ServerRequirementViewModel(requirement))
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(Source.ScenarioWorkshopId))
        {
            ScenarioRequirement = new ServerRequirementViewModel(new ServerRequirement
            {
                WorkshopId = Source.ScenarioWorkshopId,
                Title = Source.ScenarioName,
                Subtitle = string.IsNullOrWhiteSpace(Source.ScenarioSubtitle)
                    ? $"{Source.ScenarioSource} scenario"
                    : Source.ScenarioSubtitle,
                ThumbnailPath = Source.ThumbnailPath,
                IsInstalled = installedScenarioIds.Contains(Source.ScenarioWorkshopId),
                IsScenario = true
            });
        }

        MissingItems = RequiredItems
            .Where(item => !item.IsInstalled)
            .ToList();

        if (ScenarioRequirement is not null && !ScenarioRequirement.IsInstalled)
        {
            MissingItems.Insert(0, ScenarioRequirement);
        }
    }
}
