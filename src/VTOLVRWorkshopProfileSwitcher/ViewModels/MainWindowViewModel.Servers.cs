using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VTOLVRWorkshopProfileSwitcher.Models;
using VTOLVRWorkshopProfileSwitcher.Services;
using VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly ServerBrowserService _serverBrowserService = new();
    private readonly SemaphoreSlim _selectedServerRefreshLock = new(1, 1);
    private CancellationTokenSource? _selectedServerRefreshCts;
    private bool _hasLoadedServersOnce;
    private static readonly TimeSpan SelectedServerRefreshInterval = TimeSpan.FromSeconds(5);

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
    public bool HasSelectedServerScenario => SelectedServerScenario is not null;
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
            var selectedLobbyId = SelectedServer?.Source.LobbyId;
            var oldServers = Servers.ToList();

            var items = result.Servers
                .Select(server => new ServerItemViewModel(server, installedModIds, installedScenarioIds))
                .ToList();

            Servers = new ObservableCollection<ServerItemViewModel>(items);
            ServersStatusMessage = result.StatusMessage;
            ApplyServerFilter();

            if (selectedLobbyId.HasValue)
            {
                var refreshedSelection = Servers.FirstOrDefault(server => server.Source.LobbyId == selectedLobbyId.Value);
                if (refreshedSelection is not null)
                {
                    ApplySelectedServerState(refreshedSelection);
                }
            }

            DisposeItems(oldServers);
        }
        catch (Exception ex)
        {
            DisposeItems(Servers);
            Servers = new ObservableCollection<ServerItemViewModel>();
            FilteredServers = new ObservableCollection<ServerItemViewModel>();
            SelectedServer = null;
            SelectedServerScenario = null;
            SelectedServerRequirements = new ObservableCollection<ServerRequirementViewModel>();
            SelectedServerMissingItems = new ObservableCollection<ServerRequirementViewModel>();
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

        ApplySelectedServerState(server);
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

    partial void OnCurrentPageViewModelChanged(ViewModelBase? value)
    {
        SyncModDetailsVisibilityForCurrentPage(value);
        UpdateSelectedServerRefreshState();
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
        OnPropertyChanged(nameof(HasSelectedServerScenario));
        OnPropertyChanged(nameof(SelectedServerMissingSummary));
    }

    private async Task<HashSet<string>> GetInstalledModIdsForServersAsync()
    {
        var preferredPath =
            !string.IsNullOrWhiteSpace(ActiveWorkshopPath) &&
            !string.Equals(ActiveWorkshopPath, "Not detected", StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(ActiveWorkshopPath)
                ? ActiveWorkshopPath
                : null;

        return await GetInstalledWorkshopIdsForAppAsync("3018410", preferredPath);
    }

    private async Task<HashSet<string>> GetInstalledScenarioIdsAsync()
    {
        return await GetInstalledWorkshopIdsForAppAsync("667970");
    }

    private async Task<HashSet<string>> GetInstalledWorkshopIdsForAppAsync(string appId, string? preferredPath = null)
    {
        var paths = new List<string>();

        if (!string.IsNullOrWhiteSpace(preferredPath) && Directory.Exists(preferredPath))
        {
            paths.Add(preferredPath);
        }

        var detectedPaths = await _detector.FindWorkshopPathsAsync(appId);
        foreach (var path in detectedPaths)
        {
            if (!string.IsNullOrWhiteSpace(path) &&
                Directory.Exists(path) &&
                !paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        if (paths.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return await Task.Run(() =>
        {
            var installedIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var path in paths)
            {
                try
                {
                    foreach (var folderPath in Directory.EnumerateDirectories(path))
                    {
                        var folderName = Path.GetFileName(folderPath);
                        if (string.IsNullOrWhiteSpace(folderName))
                        {
                            continue;
                        }

                        if (WorkshopScanner.TryGetWorkshopId(folderName, out var workshopId, out _))
                        {
                            installedIds.Add(workshopId);
                        }
                    }
                }
                catch
                {
                    // Ignore inaccessible paths so one Steam library cannot break detection.
                }
            }

            return installedIds;
        });
    }

    private void ApplySelectedServerState(ServerItemViewModel server)
    {
        SelectedServer = server;
        SelectedServerScenario = server.ScenarioRequirement;
        SelectedServerRequirements = new ObservableCollection<ServerRequirementViewModel>(server.RequiredItems);
        SelectedServerMissingItems = new ObservableCollection<ServerRequirementViewModel>(server.MissingItems);
        NotifyServerUiStateChanged();
        UpdateSelectedServerRefreshState();
    }

    private void UpdateSelectedServerRefreshState()
    {
        var shouldRefresh =
            CurrentPageViewModel is ServerDetailsPageViewModel &&
            SelectedServer is not null &&
            HasSelectedServerMissingItems;

        if (!shouldRefresh)
        {
            StopSelectedServerRefreshLoop();
            return;
        }

        StartSelectedServerRefreshLoop();
    }

    private void SyncModDetailsVisibilityForCurrentPage(ViewModelBase? pageViewModel)
    {
        if (ShouldShowModDetailsForPage(pageViewModel))
        {
            IsDetailsOpen = SelectedMod is not null;
            return;
        }

        IsDetailsOpen = false;
    }

    private static bool ShouldShowModDetailsForPage(ViewModelBase? pageViewModel)
    {
        return pageViewModel is ModsPageViewModel or ProfileDetailsPageViewModel;
    }

    private void StartSelectedServerRefreshLoop()
    {
        if (_selectedServerRefreshCts is not null &&
            !_selectedServerRefreshCts.IsCancellationRequested)
        {
            return;
        }

        _selectedServerRefreshCts?.Dispose();
        _selectedServerRefreshCts = new CancellationTokenSource();
        _ = RunSelectedServerRefreshLoopAsync(_selectedServerRefreshCts.Token);
    }

    private void StopSelectedServerRefreshLoop()
    {
        if (_selectedServerRefreshCts is null)
        {
            return;
        }

        _selectedServerRefreshCts.Cancel();
        _selectedServerRefreshCts.Dispose();
        _selectedServerRefreshCts = null;
    }

    private async Task RunSelectedServerRefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(SelectedServerRefreshInterval, cancellationToken);
                await RefreshSelectedServerInstallStateAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshSelectedServerInstallStateAsync(CancellationToken cancellationToken)
    {
        var selectedServerSnapshot = SelectedServer;
        if (selectedServerSnapshot is null)
        {
            return;
        }

        if (!await _selectedServerRefreshLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var installedModIds = await GetInstalledModIdsForServersAsync();
            var installedScenarioIds = await GetInstalledScenarioIdsAsync();
            var refreshedServer = new ServerItemViewModel(selectedServerSnapshot.Source, installedModIds, installedScenarioIds);

            if (!HasServerInstallStateChanged(selectedServerSnapshot, refreshedServer))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReplaceServerItem(refreshedServer);

                if (SelectedServer is not null &&
                    SelectedServer.Source.LobbyId == refreshedServer.Source.LobbyId)
                {
                    ApplySelectedServerState(refreshedServer);
                    if (SelectedServerMissingItems.Count == 0)
                    {
                        StatusMessage = $"'{refreshedServer.ServerName}' now looks ready locally.";
                    }
                }
            });
        }
        finally
        {
            _selectedServerRefreshLock.Release();
        }
    }

    private bool HasServerInstallStateChanged(ServerItemViewModel current, ServerItemViewModel refreshed)
    {
        var currentMissingIds = current.MissingItems
            .Select(item => item.WorkshopId)
            .ToArray();
        var refreshedMissingIds = refreshed.MissingItems
            .Select(item => item.WorkshopId)
            .ToArray();

        if (!currentMissingIds.SequenceEqual(refreshedMissingIds, StringComparer.Ordinal))
        {
            return true;
        }

        var currentScenarioInstalled = current.ScenarioRequirement?.IsInstalled ?? true;
        var refreshedScenarioInstalled = refreshed.ScenarioRequirement?.IsInstalled ?? true;
        return currentScenarioInstalled != refreshedScenarioInstalled;
    }

    private void ReplaceServerItem(ServerItemViewModel refreshedServer)
    {
        var oldServerItems = new List<ServerItemViewModel>(2);
        ReplaceServerItemInCollection(Servers, refreshedServer, oldServerItems);
        ReplaceServerItemInCollection(FilteredServers, refreshedServer, oldServerItems);
        DisposeItems(oldServerItems);
        NotifyServerUiStateChanged();
    }

    private static void ReplaceServerItemInCollection(
        ObservableCollection<ServerItemViewModel> collection,
        ServerItemViewModel refreshedServer,
        List<ServerItemViewModel> replacedItems)
    {
        for (var i = 0; i < collection.Count; i++)
        {
            if (collection[i].Source.LobbyId == refreshedServer.Source.LobbyId)
            {
                replacedItems.Add(collection[i]);
                collection[i] = refreshedServer;
                return;
            }
        }
    }
}
