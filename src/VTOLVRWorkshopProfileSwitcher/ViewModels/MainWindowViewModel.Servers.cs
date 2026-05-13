using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VTOLVRWorkshopProfileSwitcher.Models;
using VTOLVRWorkshopProfileSwitcher.Services;
using VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly ServerBrowserService _serverBrowserService = new();
    private bool _hasLoadedServersOnce;

    [ObservableProperty]
    private ObservableCollection<ServerItemViewModel> servers = new();

    [ObservableProperty]
    private ObservableCollection<ServerItemViewModel> filteredServers = new();

    [ObservableProperty]
    private string serverSearchQuery = string.Empty;

    [ObservableProperty]
    private bool includePasswordServers;

    [ObservableProperty]
    private bool showModdedServersOnly;

    [ObservableProperty]
    private bool isLoadingServers;

    [ObservableProperty]
    private string serversStatusMessage = "Start Steam to view servers.";

    [ObservableProperty]
    private ObservableCollection<ServerRequirementViewModel> selectedServerRequirements = new();

    [ObservableProperty]
    private ObservableCollection<ServerRequirementViewModel> selectedServerMissingItems = new();

    [ObservableProperty]
    private ServerRequirementViewModel? selectedServerScenario;

    public bool HasServerResults => FilteredServers.Count > 0;
    public bool ShowServerEmptyState => !IsLoadingServers && FilteredServers.Count == 0;
    public string ServerCountSummary => $"{FilteredServers.Count} shown / {Servers.Count} total";
    public bool HasSelectedServerRequirements => SelectedServerRequirements.Count > 0;
    public bool ShowSelectedServerRequirementsEmpty => SelectedServerRequirements.Count == 0;
    public bool HasSelectedServerMissingItems => SelectedServerMissingItems.Count > 0;
    public string SelectedServerMissingSummary => SelectedServerMissingItems.Count == 0
        ? "Everything required by this server appears to be installed."
        : $"{SelectedServerMissingItems.Count} item(s) are missing locally. Open them in Steam Workshop to install.";

    partial void OnServerSearchQueryChanged(string value)
    {
        ApplyServerFilter();
    }

    partial void OnIncludePasswordServersChanged(bool value)
    {
        ApplyServerFilter();
    }

    partial void OnShowModdedServersOnlyChanged(bool value)
    {
        ApplyServerFilter();
    }

    private void ApplyServerFilter()
    {
        IEnumerable<ServerItemViewModel> working = Servers;

        if (!IncludePasswordServers)
        {
            working = working.Where(server => !server.IsPasswordProtected);
        }

        if (ShowModdedServersOnly)
        {
            working = working.Where(server => server.IsModded);
        }

        var query = ServerSearchQuery.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            working = working.Where(server =>
                server.ServerName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                server.CreatorName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                server.ScenarioName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredServers = new ObservableCollection<ServerItemViewModel>(working);
        NotifyServerUiStateChanged();
    }

    [RelayCommand]
    private async Task RefreshServersAsync()
    {
        if (IsLoadingServers)
        {
            return;
        }

        IsLoadingServers = true;
        ServersStatusMessage = "Refreshing public VTOL VR servers...";
        NotifyServerUiStateChanged();

        try
        {
            var result = await _serverBrowserService.LoadServersAsync();
            _hasLoadedServersOnce = true;
            var installedModIds = await GetInstalledModIdsForServersAsync();
            var installedScenarioIds = await GetInstalledScenarioIdsAsync();

            var items = result.Servers
                .Select(server => new ServerItemViewModel(server, installedModIds, installedScenarioIds))
                .ToList();

            Servers = new ObservableCollection<ServerItemViewModel>(items);
            ServersStatusMessage = result.StatusMessage;
            ApplyServerFilter();
        }
        catch (Exception ex)
        {
            Servers = new ObservableCollection<ServerItemViewModel>();
            FilteredServers = new ObservableCollection<ServerItemViewModel>();
            ServersStatusMessage = $"Server refresh failed: {ex.Message}";
            NotifyServerUiStateChanged();
            await _logger.LogAsync($"Server refresh failed: {ex.Message}");
        }
        finally
        {
            IsLoadingServers = false;
            NotifyServerUiStateChanged();
        }
    }

    private async Task LoadServersIfNeededAsync()
    {
        if (_hasLoadedServersOnce || IsLoadingServers)
        {
            return;
        }

        await RefreshServersAsync();
    }

    [RelayCommand]
    private void OpenServerDetails(ServerItemViewModel? server)
    {
        if (server is null)
        {
            return;
        }

        SelectedServer = server;
        SelectedServerScenario = server.ScenarioRequirement;
        SelectedServerRequirements = new ObservableCollection<ServerRequirementViewModel>(server.RequiredItems);
        SelectedServerMissingItems = new ObservableCollection<ServerRequirementViewModel>(server.MissingItems);
        NotifyServerUiStateChanged();
        SelectedNavItem = NavItem.Servers;
        CurrentPageViewModel = new ServerDetailsPageViewModel(this, server);
    }

    [RelayCommand]
    private async Task CreateModpackForServerAsync(ServerItemViewModel? server)
    {
        server ??= SelectedServer;
        if (server is null)
        {
            StatusMessage = "No server selected";
            return;
        }

        var requiredIds = server.RequiredItems
            .Select(item => item.WorkshopId)
            .Where(IsNumericWorkshopId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (requiredIds.Count == 0)
        {
            StatusMessage = "This server does not expose any required mod items";
            return;
        }

        var existing = await _profileService.LoadProfilesAsync();
        var baseName = $"Server - {server.ServerName}";
        var profileName = baseName;
        var suffix = 2;
        while (existing.Any(profile => string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase)))
        {
            profileName = $"{baseName} ({suffix++})";
        }

        var notes = $"Creator: {server.CreatorName} | Scenario: {server.ScenarioName} | Join Code: {server.JoinCode}";
        var profile = new ModProfile
        {
            Name = profileName,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            EnabledMods = requiredIds,
            IncludedMods = requiredIds,
            IconKind = nameof(Material.Icons.MaterialIconKind.Server)
        };

        await _profileService.SaveProfileAsync(profile);
        await LoadProfilesAsync();
        await _logger.LogAsync($"Created server profile '{profileName}' from lobby '{server.ServerName}'");
        StatusMessage = $"Created profile '{profileName}'";
    }

    [RelayCommand]
    private void BackToServers()
    {
        if (_serversPage is null)
        {
            InitializeShell();
        }

        SelectedNavItem = NavItem.Servers;
        CurrentPageViewModel = _serversPage;
    }

    [RelayCommand]
    private async Task OpenAllMissingServerItemsAsync()
    {
        if (SelectedServerMissingItems.Count == 0)
        {
            StatusMessage = "No missing server items to open";
            return;
        }

        foreach (var requirement in SelectedServerMissingItems
                     .Select(item => item.WorkshopId)
                     .Distinct(StringComparer.Ordinal))
        {
            OpenSteamWorkshopPage(requirement);
            await Task.Delay(150);
        }

        StatusMessage = $"Opened {SelectedServerMissingItems.Count} missing item(s) in Steam Workshop";
    }

    [RelayCommand]
    private void OpenServerRequirementWorkshop(ServerRequirementViewModel? requirement)
    {
        if (requirement is null)
        {
            return;
        }

        OpenSteamWorkshopPage(requirement.WorkshopId);
        StatusMessage = $"Opened workshop page for {requirement.WorkshopId}";
    }

    [RelayCommand]
    private void OpenServerScenarioWorkshop()
    {
        if (SelectedServerScenario is null)
        {
            return;
        }

        OpenSteamWorkshopPage(SelectedServerScenario.WorkshopId);
        StatusMessage = $"Opened scenario workshop page for {SelectedServerScenario.WorkshopId}";
    }

    [RelayCommand]
    private async Task CopySelectedServerJoinCodeAsync()
    {
        if (SelectedServer is null || string.IsNullOrWhiteSpace(SelectedServer.JoinCode) || SelectedServer.JoinCode == "n/a")
        {
            StatusMessage = "No join code is available for this server";
            return;
        }

        var topLevel = GetMainWindow();
        if (topLevel?.Clipboard is null)
        {
            StatusMessage = "Clipboard is not available";
            return;
        }

        await topLevel.Clipboard.SetTextAsync(SelectedServer.JoinCode);
        StatusMessage = $"Copied join code {SelectedServer.JoinCode}";
    }

    private void NotifyServerUiStateChanged()
    {
        OnPropertyChanged(nameof(IsServersSelected));
        OnPropertyChanged(nameof(HasServerResults));
        OnPropertyChanged(nameof(ShowServerEmptyState));
        OnPropertyChanged(nameof(ServerCountSummary));
        OnPropertyChanged(nameof(HasSelectedServerRequirements));
        OnPropertyChanged(nameof(ShowSelectedServerRequirementsEmpty));
        OnPropertyChanged(nameof(HasSelectedServerMissingItems));
        OnPropertyChanged(nameof(SelectedServerMissingSummary));
    }

    private async Task<HashSet<string>> GetInstalledModIdsForServersAsync()
    {
        if (!string.IsNullOrWhiteSpace(ActiveWorkshopPath) &&
            !string.Equals(ActiveWorkshopPath, "Not detected", StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(ActiveWorkshopPath))
        {
            return await GetInstalledWorkshopIdsAsync();
        }

        var path = await _detector.FindFirstWorkshopPathAsync("3018410");
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return Directory.EnumerateDirectories(path)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.TrimStart('#'))
            .Where(IsNumericWorkshopId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<HashSet<string>> GetInstalledScenarioIdsAsync()
    {
        var path = await _detector.FindFirstWorkshopPathAsync("667970");
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return Directory.EnumerateDirectories(path)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.TrimStart('#'))
            .Where(IsNumericWorkshopId)
            .ToHashSet(StringComparer.Ordinal);
    }
}
