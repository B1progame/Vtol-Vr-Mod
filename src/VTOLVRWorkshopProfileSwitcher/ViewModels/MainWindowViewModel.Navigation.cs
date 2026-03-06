using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VTOLVRWorkshopProfileSwitcher.Models;
using VTOLVRWorkshopProfileSwitcher.Services;
using VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;
using VTOLVRWorkshopProfileSwitcher.Views;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed partial class MainWindowViewModel
{
    private DashboardPageViewModel? _dashboardPage;
    private ProfilesPageViewModel? _profilesPage;
    private ModsPageViewModel? _modsPage;
    private BackupsPageViewModel? _backupsPage;
    private LogsPageViewModel? _logsPage;
    private SettingsPageViewModel? _settingsPage;

    [ObservableProperty]
    private NavItem selectedNavItem;

    [ObservableProperty]
    private ViewModelBase? currentPageViewModel;

    [ObservableProperty]
    private bool isDetailsOpen;

    [ObservableProperty]
    private ModItemViewModel? selectedMod;

    [ObservableProperty]
    private ObservableCollection<BackupItemViewModel> backups = new();

    [ObservableProperty]
    private BackupItemViewModel? selectedBackup;

    [ObservableProperty]
    private ObservableCollection<LogEntryViewModel> allLogs = new();

    [ObservableProperty]
    private ObservableCollection<LogEntryViewModel> recentLogs = new();

    [ObservableProperty]
    private LogEntryViewModel? selectedLogEntry;

    [ObservableProperty]
    private bool showInfoLogs = true;

    [ObservableProperty]
    private bool showWarningLogs = true;

    [ObservableProperty]
    private bool showErrorLogs = true;

    [ObservableProperty]
    private ProfileItemViewModel? profileUnderEdit;

    [ObservableProperty]
    private ProfileModEntryViewModel? selectedProfileModEntry;

    [ObservableProperty]
    private bool selectedProfileModActiveInProfile;

    [ObservableProperty]
    private bool isAddModSelectionMode;

    [ObservableProperty]
    private string profileModsSearchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ProfileModEntryViewModel> filteredProfileMods = new();

    [ObservableProperty]
    private bool showProfileDependencies = true;

    public int TotalModsCount => _allMods.Count;
    public int EnabledModsCount => _allMods.Count(m => m.IsEnabled);
    public int UpdatesAvailableCount => 0;
    public bool IsDashboardSelected => SelectedNavItem == NavItem.Dashboard;
    public bool IsProfilesSelected => SelectedNavItem == NavItem.Profiles;
    public bool IsModsSelected => SelectedNavItem == NavItem.Mods;
    public bool IsBackupsSelected => SelectedNavItem == NavItem.Backups;
    public bool IsLogsSelected => SelectedNavItem == NavItem.Logs;
    public bool IsSettingsSelected => SelectedNavItem == NavItem.Settings;
    public bool HasProfileUnderEdit => ProfileUnderEdit is not null;
    public bool HasSelectedProfileModEntry =>
        ProfileUnderEdit is not null &&
        SelectedProfileModEntry is not null &&
        !SelectedProfileModEntry.IsDependencyOnly;
    public string ProfileUnderEditText => ProfileUnderEdit is null ? "No profile selected" : $"Editing: {ProfileUnderEdit.Name}";
    public bool CanLaunchGame => true;
    public bool CanOpenLaunchFlyout => !IsLaunchingGame && !IsVtolRunning;
    public string SidebarPlayText => IsLaunchingGame ? "Cancel" : (IsVtolRunning ? "STOP" : "Play");
    public string PlayModdedButtonText => GetLaunchButtonText("PLAY (MODDED)", LaunchHoverTargetModded);
    public string PlayVanillaButtonText => GetLaunchButtonText("PLAY (VANILLA)", LaunchHoverTargetVanilla);
    public string SelectedModTitle => SelectedMod?.ModName ?? "No Mod Selected";
    public string SelectedModAuthor => _selectedModAuthor;
    public string SelectedModLastUpdated => _selectedModLastUpdated;
    public string SelectedModWorkshopId => SelectedMod?.WorkshopId ?? "n/a";
    public string SelectedModLocalVersion => _selectedModLocalVersion;
    public string SelectedModRemoteVersion => _selectedModRemoteVersion;
    public string SelectedModSize => _selectedModSize;
    public string SelectedModStatus => _selectedModStatus;
    public bool SelectedModHasUpdate => _selectedModHasUpdate;

    private string _selectedModAuthor = "n/a";
    private string _selectedModLastUpdated = "n/a";
    private string _selectedModLocalVersion = "n/a";
    private string _selectedModRemoteVersion = "n/a";
    private string _selectedModSize = "n/a";
    private string _selectedModStatus = "n/a";
    private bool _selectedModHasUpdate;
    private bool _isSyncingSelectedProfileModState;
    private List<ProfileModEntryViewModel> _currentProfileModEntries = new();
    private CancellationTokenSource? _profileDependencyToggleCts;
    private List<LogEntryViewModel> _rawLogs = new();

    private void InitializeShell()
    {
        _dashboardPage = new DashboardPageViewModel(this);
        _profilesPage = new ProfilesPageViewModel(this);
        _modsPage = new ModsPageViewModel(this);
        _backupsPage = new BackupsPageViewModel(this);
        _logsPage = new LogsPageViewModel(this);
        _settingsPage = new SettingsPageViewModel(this);

        SelectedNavItem = NavItem.Dashboard;
        CurrentPageViewModel = _dashboardPage;
        IsDetailsOpen = false;
    }

    private async Task LoadShellDataAsync()
    {
        await ReloadBackupsAsync();
        await ReloadLogsAsync();
        NotifyModStatsChanged();
    }

    private void NotifyModStatsChanged()
    {
        OnPropertyChanged(nameof(TotalModsCount));
        OnPropertyChanged(nameof(EnabledModsCount));
    }

    partial void OnSelectedNavItemChanged(NavItem value)
    {
        OnPropertyChanged(nameof(IsDashboardSelected));
        OnPropertyChanged(nameof(IsProfilesSelected));
        OnPropertyChanged(nameof(IsModsSelected));
        OnPropertyChanged(nameof(IsBackupsSelected));
        OnPropertyChanged(nameof(IsLogsSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
    }

    partial void OnIsDetailsOpenChanged(bool value)
    {
    }

    partial void OnIsAddModSelectionModeChanged(bool value)
    {
        if (!value)
        {
            CancelAddModeProfileSave();
        }
    }

    partial void OnIsLaunchingGameChanged(bool value)
    {
        if (!value)
        {
            LaunchButtonHoverTarget = string.Empty;
        }

        OnPropertyChanged(nameof(CanLaunchGame));
        OnPropertyChanged(nameof(CanOpenLaunchFlyout));
        OnPropertyChanged(nameof(SidebarPlayText));
        OnPropertyChanged(nameof(PlayModdedButtonText));
        OnPropertyChanged(nameof(PlayVanillaButtonText));
    }

    partial void OnIsVtolRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanOpenLaunchFlyout));
        OnPropertyChanged(nameof(SidebarPlayText));
        OnPropertyChanged(nameof(PlayModdedButtonText));
        OnPropertyChanged(nameof(PlayVanillaButtonText));
    }

    partial void OnLaunchButtonHoverTargetChanged(string value)
    {
        OnPropertyChanged(nameof(SidebarPlayText));
        OnPropertyChanged(nameof(PlayModdedButtonText));
        OnPropertyChanged(nameof(PlayVanillaButtonText));
    }

    public void SetLaunchButtonHovered(string launchTarget, bool isHovered)
    {
        if (!IsLaunchingGame)
        {
            LaunchButtonHoverTarget = string.Empty;
            return;
        }

        if (isHovered)
        {
            LaunchButtonHoverTarget = launchTarget;
            return;
        }

        if (string.Equals(LaunchButtonHoverTarget, launchTarget, StringComparison.Ordinal))
        {
            LaunchButtonHoverTarget = string.Empty;
        }
    }

    private string GetLaunchButtonText(string idleText, string launchTarget)
    {
        if (!IsLaunchingGame)
        {
            return IsVtolRunning ? "STOP" : idleText;
        }

        return string.Equals(LaunchButtonHoverTarget, launchTarget, StringComparison.Ordinal)
            ? "Cancel"
            : "Launching...";
    }

    private void RequestLaunchCancel(string mode)
    {
        if (!IsLaunchingGame || _launchCts is null || _launchCts.IsCancellationRequested)
        {
            return;
        }

        _launchCts.Cancel();
        StatusMessage = $"Canceling {mode} launch...";
    }

    private bool IsLaunchCanceled(CancellationToken token, string canceledMessage)
    {
        if (!token.IsCancellationRequested)
        {
            return false;
        }

        StatusMessage = canceledMessage;
        return true;
    }

    private CancellationToken BeginLaunch()
    {
        _launchCts?.Cancel();
        _launchCts?.Dispose();
        _launchCts = new CancellationTokenSource();
        return _launchCts.Token;
    }

    private void EndLaunch(CancellationToken token)
    {
        LaunchButtonHoverTarget = string.Empty;
        IsLaunchingGame = false;

        if (_launchCts is null || _launchCts.Token != token)
        {
            return;
        }

        _launchCts.Dispose();
        _launchCts = null;
    }

    partial void OnSelectedModChanged(ModItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedModTitle));
        OnPropertyChanged(nameof(SelectedModWorkshopId));

        UpdateSelectedModDetailFields(value);

        if (value is not null)
        {
            IsDetailsOpen = true;
        }
        else
        {
            IsDetailsOpen = false;
        }
    }

    partial void OnProfileUnderEditChanged(ProfileItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasProfileUnderEdit));
        OnPropertyChanged(nameof(HasSelectedProfileModEntry));
        OnPropertyChanged(nameof(ProfileUnderEditText));
    }

    partial void OnSelectedProfileModEntryChanged(ProfileModEntryViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedProfileModEntry));
        _isSyncingSelectedProfileModState = true;
        try
        {
            SelectedProfileModActiveInProfile = value?.IsActiveInProfile ?? false;
        }
        finally
        {
            _isSyncingSelectedProfileModState = false;
        }
    }

    partial void OnSelectedProfileModActiveInProfileChanged(bool value)
    {
        if (_isSyncingSelectedProfileModState || SelectedProfileModEntry is null)
        {
            return;
        }

        _ = SetSelectedProfileModActiveInProfileAsync(value);
    }

    partial void OnProfileModsSearchQueryChanged(string value)
    {
        ApplyProfileModsFilter();
    }

    partial void OnShowProfileDependenciesChanged(bool value)
    {
        if (ProfileUnderEdit is null)
        {
            return;
        }

        _profileDependencyToggleCts?.Cancel();
        _profileDependencyToggleCts?.Dispose();
        _profileDependencyToggleCts = new CancellationTokenSource();
        _ = RefreshProfileDependenciesToggleAsync(ProfileUnderEdit, value, _profileDependencyToggleCts.Token);
    }

    partial void OnShowInfoLogsChanged(bool value)
    {
        ApplyLogFilter();
    }

    partial void OnShowWarningLogsChanged(bool value)
    {
        ApplyLogFilter();
    }

    partial void OnShowErrorLogsChanged(bool value)
    {
        ApplyLogFilter();
    }

    public ObservableCollection<ProfileModEntryViewModel> BuildProfileModEntries(ProfileItemViewModel profile)
    {
        var map = _allMods.ToDictionary(m => m.WorkshopId, StringComparer.Ordinal);
        var enabledSet = profile.EnabledMods.ToHashSet(StringComparer.Ordinal);
        var items = new List<ProfileModEntryViewModel>();

        foreach (var id in GetIncludedProfileModIds(profile.Source))
        {
            if (map.TryGetValue(id, out var mod))
            {
                items.Add(new ProfileModEntryViewModel(id, mod.ModName, true, enabledSet.Contains(id), false, mod));
            }
            else
            {
                items.Add(new ProfileModEntryViewModel(id, $"Missing mod {id}", false, enabledSet.Contains(id), false, null));
            }
        }

        return new ObservableCollection<ProfileModEntryViewModel>(items);
    }

    private static List<string> GetIncludedProfileModIds(ModProfile profile)
    {
        var included = profile.IncludedMods.Count == 0 ? profile.EnabledMods : profile.IncludedMods;
        return included.Distinct(StringComparer.Ordinal).ToList();
    }

    [RelayCommand]
    private void NavigateDashboard()
    {
        IsAddModSelectionMode = false;
        if (_dashboardPage is null)
        {
            InitializeShell();
        }

        SelectedNavItem = NavItem.Dashboard;
        CurrentPageViewModel = _dashboardPage;
    }

    [RelayCommand]
    private void NavigateProfiles()
    {
        IsAddModSelectionMode = false;
        if (_profilesPage is null)
        {
            InitializeShell();
        }

        SelectedNavItem = NavItem.Profiles;
        CurrentPageViewModel = _profilesPage;
    }

    [RelayCommand]
    private void NavigateMods()
    {
        IsAddModSelectionMode = false;
        if (_modsPage is null)
        {
            InitializeShell();
        }

        SelectedNavItem = NavItem.Mods;
        CurrentPageViewModel = _modsPage;
    }

    [RelayCommand]
    private async Task NavigateBackupsAsync()
    {
        IsAddModSelectionMode = false;
        if (_backupsPage is null)
        {
            InitializeShell();
        }

        await ReloadBackupsAsync();
        SelectedNavItem = NavItem.Backups;
        CurrentPageViewModel = _backupsPage;
    }

    [RelayCommand]
    private async Task NavigateLogsAsync()
    {
        IsAddModSelectionMode = false;
        if (_logsPage is null)
        {
            InitializeShell();
        }

        await ReloadLogsAsync();
        SelectedNavItem = NavItem.Logs;
        CurrentPageViewModel = _logsPage;
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        IsAddModSelectionMode = false;
        if (_settingsPage is null)
        {
            InitializeShell();
        }

        SelectedNavItem = NavItem.Settings;
        CurrentPageViewModel = _settingsPage;
    }

    [RelayCommand]
    private void OpenProfileDetails(ProfileItemViewModel? profile)
    {
        if (profile is null)
        {
            return;
        }

        SelectedProfileModEntry = null;
        _ = RefreshProfileModEntriesAsync(profile);
        SelectedProfile = profile;
        ProfileUnderEdit = profile;
        ProfileNameInput = profile.Name;
        ProfileNotesInput = profile.Notes;
        SelectedNavItem = NavItem.Profiles;
        CurrentPageViewModel = new ProfileDetailsPageViewModel(this, profile);
    }

    [RelayCommand]
    private void BackToProfiles()
    {
        NavigateProfiles();
    }

    [RelayCommand]
    private void ToggleDetails()
    {
        IsDetailsOpen = !IsDetailsOpen;
    }

    [RelayCommand]
    private void SelectMod(ModItemViewModel? mod)
    {
        if (mod is null)
        {
            return;
        }

        if (ReferenceEquals(SelectedMod, mod))
        {
            IsDetailsOpen = !IsDetailsOpen;
            return;
        }

        SelectedProfileModEntry = null;
        SelectedMod = mod;
        IsDetailsOpen = true;
    }

    [RelayCommand]
    private void SelectProfileMod(ProfileModEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        SelectedProfileModEntry = entry;
        if (entry.InstalledMod is null)
        {
            SelectedMod = null;
            StatusMessage = "Mod is missing locally";
            IsDetailsOpen = true;
            return;
        }

        SelectedMod = entry.InstalledMod;
        IsDetailsOpen = true;
    }

    [RelayCommand]
    private void OpenSelectedModWorkshop()
    {
        OpenModWorkshopPage(SelectedMod);
    }

    [RelayCommand]
    private void UpdateSelectedMod()
    {
        if (SelectedMod is null)
        {
            return;
        }

        OpenModWorkshopPage(SelectedMod);
        StatusMessage = $"Opened workshop page for {SelectedMod.WorkshopId}";
    }

    [RelayCommand]
    private void OpenAddModsPage()
    {
        const string webUrl = "https://steamcommunity.com/app/3018410/workshop/";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = webUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            StatusMessage = "Failed to open workshop page";
        }
    }

    [RelayCommand]
    private async Task RenameSelectedProfileAsync()
    {
        if (ProfileUnderEdit is null)
        {
            StatusMessage = "No profile selected";
            return;
        }

        var newName = ProfileNameInput.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            StatusMessage = "Profile name is required";
            return;
        }

        var old = ProfileUnderEdit.Source;
        var renamed = new ModProfile
        {
            Name = newName,
            Notes = ProfileNotesInput.Trim(),
            CreatedAt = old.CreatedAt,
            EnabledMods = old.EnabledMods.Distinct(StringComparer.Ordinal).ToList(),
            IncludedMods = GetIncludedProfileModIds(old)
        };

        if (!string.Equals(old.Name, newName, StringComparison.Ordinal))
        {
            await _profileService.DeleteProfileAsync(old.Name);
        }

        await _profileService.SaveProfileAsync(renamed);
        await _logger.LogAsync($"Renamed profile '{old.Name}' to '{newName}'");
        await LoadProfilesAsync();

        var selected = Profiles.FirstOrDefault(p => string.Equals(p.Name, newName, StringComparison.Ordinal));
        if (selected is not null)
        {
            OpenProfileDetails(selected);
        }
    }

    [RelayCommand]
    private async Task AddSelectedModToCurrentProfileAsync(ModItemViewModel? mod)
    {
        if (mod is null)
        {
            return;
        }

        var profile = await PickProfileAsync();
        if (profile is null)
        {
            StatusMessage = "Add canceled (no profile selected)";
            return;
        }

        var source = profile.Source;
        var includedIds = GetIncludedProfileModIds(source);
        var enabledIds = source.EnabledMods.Distinct(StringComparer.Ordinal).ToList();
        var isIncluded = includedIds.Contains(mod.WorkshopId, StringComparer.Ordinal);
        var isEnabled = enabledIds.Contains(mod.WorkshopId, StringComparer.Ordinal);

        if (isIncluded && isEnabled)
        {
            StatusMessage = $"Mod {mod.WorkshopId} already in profile";
            return;
        }

        if (!isIncluded)
        {
            includedIds.Add(mod.WorkshopId);
        }

        if (!isEnabled)
        {
            enabledIds.Add(mod.WorkshopId);
        }

        var updated = new ModProfile
        {
            Name = source.Name,
            Notes = source.Notes,
            CreatedAt = source.CreatedAt,
            EnabledMods = enabledIds.Distinct(StringComparer.Ordinal).ToList(),
            IncludedMods = includedIds.Distinct(StringComparer.Ordinal).ToList()
        };

        await _profileService.SaveProfileAsync(updated);
        await _logger.LogAsync($"{(isIncluded ? "Re-activated" : "Added")} mod {mod.WorkshopId} in profile '{source.Name}'");
        await LoadProfilesAsync();

        var selected = Profiles.FirstOrDefault(p => string.Equals(p.Name, source.Name, StringComparison.Ordinal));
        if (selected is not null)
        {
            OpenProfileDetails(selected);
            StatusMessage = $"{(isIncluded ? "Re-activated" : "Added")} mod {mod.WorkshopId} to profile '{source.Name}'";
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedModFromCurrentProfileAsync(ProfileModEntryViewModel? entry)
    {
        if (ProfileUnderEdit is null || entry is null)
        {
            return;
        }

        var source = ProfileUnderEdit.Source;
        var includedIds = GetIncludedProfileModIds(source);
        if (!includedIds.Contains(entry.WorkshopId, StringComparer.Ordinal))
        {
            StatusMessage = $"Mod {entry.WorkshopId} is not in profile";
            return;
        }

        var updated = new ModProfile
        {
            Name = source.Name,
            Notes = source.Notes,
            CreatedAt = source.CreatedAt,
            EnabledMods = source.EnabledMods
                .Where(id => !string.Equals(id, entry.WorkshopId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            IncludedMods = includedIds
                .Where(id => !string.Equals(id, entry.WorkshopId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };

        await _profileService.SaveProfileAsync(updated);
        await _logger.LogAsync($"Removed mod {entry.WorkshopId} from profile '{source.Name}'");
        await LoadProfilesAsync();

        var selected = Profiles.FirstOrDefault(p => string.Equals(p.Name, source.Name, StringComparison.Ordinal));
        if (selected is not null)
        {
            SelectedProfileModEntry = null;
            OpenProfileDetails(selected);
            StatusMessage = $"Removed mod {entry.WorkshopId} from profile";
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedProfileModFromDetailsAsync()
    {
        await RemoveSelectedModFromCurrentProfileAsync(SelectedProfileModEntry);
    }

    [RelayCommand]
    private async Task ReloadProfileModsAndCheckDependenciesAsync()
    {
        await RefreshModsAsync();
        if (ProfileUnderEdit is null)
        {
            StatusMessage = "Reloaded mods";
            return;
        }

        if (string.IsNullOrWhiteSpace(ActiveWorkshopPath) || ActiveWorkshopPath == "Not detected")
        {
            StatusMessage = "Workshop path not set";
            return;
        }

        var requiredOrderedIds = ProfileUnderEdit.Source.EnabledMods
            .Where(IsNumericWorkshopId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var requestedEnabledSet = await BuildRequestedEnabledSetWithCwbInferenceAsync(requiredOrderedIds);
        var dependencyResult = await _dependencyResolver.ResolveAsync(ActiveWorkshopPath, requestedEnabledSet);
        var installedIds = _allMods
            .Select(mod => mod.WorkshopId)
            .Where(IsNumericWorkshopId)
            .ToHashSet(StringComparer.Ordinal);
        var missingRequiredIds = BuildMissingWorkshopIdList(requiredOrderedIds, installedIds);
        var missingDependencyIds = dependencyResult.MissingDependencyIds
            .Where(IsNumericWorkshopId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var combinedMissing = missingRequiredIds
            .Concat(missingDependencyIds)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await SetMissingModsAsync(combinedMissing, $"profile '{ProfileUnderEdit.Name}' dependency check");
        if (combinedMissing.Count == 0)
        {
            OpenProfileDetails(ProfileUnderEdit);
            StatusMessage = "Reloaded mods and dependency check passed";
            return;
        }

        NavigateMods();
        await _logger.LogAsync(
            $"Dependency check missing ids for profile '{ProfileUnderEdit.Name}': {string.Join(", ", combinedMissing)}");
        StatusMessage = $"Dependency check found {combinedMissing.Count} missing IDs. Use 'Open Next Missing'.";
    }

    private async Task SetSelectedProfileModActiveInProfileAsync(bool isActive)
    {
        if (ProfileUnderEdit is null || SelectedProfileModEntry is null)
        {
            return;
        }

        var source = ProfileUnderEdit.Source;
        var workshopId = SelectedProfileModEntry.WorkshopId;
        var includedIds = GetIncludedProfileModIds(source);
        if (!includedIds.Contains(workshopId, StringComparer.Ordinal))
        {
            StatusMessage = $"Mod {workshopId} is not in profile";
            return;
        }

        var enabledIds = source.EnabledMods.Distinct(StringComparer.Ordinal).ToList();
        if (isActive && !enabledIds.Contains(workshopId, StringComparer.Ordinal))
        {
            enabledIds.Add(workshopId);
        }
        else if (!isActive)
        {
            enabledIds = enabledIds
                .Where(id => !string.Equals(id, workshopId, StringComparison.Ordinal))
                .ToList();
        }

        var updated = new ModProfile
        {
            Name = source.Name,
            Notes = source.Notes,
            CreatedAt = source.CreatedAt,
            EnabledMods = enabledIds.Distinct(StringComparer.Ordinal).ToList(),
            IncludedMods = includedIds.Distinct(StringComparer.Ordinal).ToList()
        };

        await _profileService.SaveProfileAsync(updated);
        await _logger.LogAsync($"{(isActive ? "Activated" : "Deactivated")} mod {workshopId} in profile '{source.Name}'");
        await LoadProfilesAsync();

        var selected = Profiles.FirstOrDefault(p => string.Equals(p.Name, source.Name, StringComparison.Ordinal));
        if (selected is null)
        {
            return;
        }

        OpenProfileDetails(selected);
        var refreshedEntry = _currentProfileModEntries
            .FirstOrDefault(item => string.Equals(item.WorkshopId, workshopId, StringComparison.Ordinal));
        SelectedProfileModEntry = refreshedEntry;
        StatusMessage = isActive
            ? $"Activated mod {workshopId} in profile"
            : $"Deactivated mod {workshopId} in profile";
    }

    private void ApplyProfileModsFilter()
    {
        var query = ProfileModsSearchQuery?.Trim() ?? string.Empty;
        IEnumerable<ProfileModEntryViewModel> working = _currentProfileModEntries;

        if (!string.IsNullOrWhiteSpace(query))
        {
            working = working.Where(m =>
                m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.WorkshopId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredProfileMods = new ObservableCollection<ProfileModEntryViewModel>(
            working.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase));
    }

    private async Task RefreshProfileModEntriesAsync(ProfileItemViewModel profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = BuildProfileModEntries(profile).ToList();
        if (ShowProfileDependencies &&
            !string.IsNullOrWhiteSpace(ActiveWorkshopPath) &&
            ActiveWorkshopPath != "Not detected")
        {
            try
            {
                var requestedOrderedIds = profile.Source.EnabledMods
                    .Where(IsNumericWorkshopId)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                var requestedSet = await BuildRequestedEnabledSetWithCwbInferenceAsync(requestedOrderedIds, cancellationToken);
                var dependencyResult = await _dependencyResolver.ResolveAsync(ActiveWorkshopPath, requestedSet, cancellationToken);
                var map = _allMods.ToDictionary(m => m.WorkshopId, StringComparer.Ordinal);
                var existingIds = entries
                    .Select(entry => entry.WorkshopId)
                    .ToHashSet(StringComparer.Ordinal);

                var dependencyOnlyIds = dependencyResult.EnabledWorkshopIds
                    .Where(IsNumericWorkshopId)
                    .Where(id => !existingIds.Contains(id))
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList();

                foreach (var dependencyId in dependencyOnlyIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (map.TryGetValue(dependencyId, out var mod))
                    {
                        entries.Add(new ProfileModEntryViewModel(
                            dependencyId,
                            mod.ModName,
                            true,
                            false,
                            true,
                            mod));
                    }
                    else
                    {
                        entries.Add(new ProfileModEntryViewModel(
                            dependencyId,
                            $"Missing dependency {dependencyId}",
                            false,
                            false,
                            true,
                            null));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Ignore dependency expansion failures in UI projection.
            }
        }

        _currentProfileModEntries = entries;
        ApplyProfileModsFilter();
    }

    private async Task RefreshProfileDependenciesToggleAsync(
        ProfileItemViewModel profile,
        bool showDependencies,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!showDependencies)
            {
                _currentProfileModEntries = _currentProfileModEntries
                    .Where(entry => !entry.IsDependencyOnly)
                    .ToList();
                ApplyProfileModsFilter();
                StatusMessage = "Auto dependencies hidden";
                return;
            }

            await RefreshProfileModEntriesAsync(profile, cancellationToken);
            StatusMessage = showDependencies
                ? "Auto dependencies enabled"
                : "Auto dependencies hidden";
        }
        catch (OperationCanceledException)
        {
            // Ignore stale toggle requests.
        }
        catch
        {
            StatusMessage = "Failed to reload after dependency toggle";
        }
    }

    [RelayCommand]
    private void OpenAddModFlow()
    {
        if (ProfileUnderEdit is not null)
        {
            LoadProfileModsIntoToggleSelection(ProfileUnderEdit);
        }

        NavigateMods();
        IsAddModSelectionMode = true;
        if (ProfileUnderEdit is null)
        {
            StatusMessage = "Pick a mod and use Add To Profile";
        }
        else
        {
            StatusMessage = "Add Mod mode: toggles are visible for profile selection";
        }
    }

    private void LoadProfileModsIntoToggleSelection(ProfileItemViewModel profile)
    {
        var requestedOrderedIds = profile.EnabledMods
            .Where(IsNumericWorkshopId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var requestedSet = requestedOrderedIds.ToHashSet(StringComparer.Ordinal);
        var installedIds = _allMods
            .Select(mod => mod.WorkshopId)
            .Where(IsNumericWorkshopId)
            .ToHashSet(StringComparer.Ordinal);
        var missingIds = BuildMissingWorkshopIdList(requestedOrderedIds, installedIds);

        _isLoadingProfileSelectionIntoToggles = true;
        try
        {
            foreach (var mod in _allMods)
            {
                mod.IsEnabled = requestedSet.Contains(mod.WorkshopId);
            }
        }
        finally
        {
            _isLoadingProfileSelectionIntoToggles = false;
        }

        _ = SetMissingModsAsync(missingIds, $"profile '{profile.Name}' selection");

        StatusMessage = missingIds.Count == 0
            ? $"Loaded {requestedSet.Count} profile mods into selection"
            : $"Loaded {requestedSet.Count} profile mods ({missingIds.Count} missing locally)";
    }

    [RelayCommand]
    private Task ReloadBackupsAsync()
    {
        var backupFiles = Directory.Exists(_appPaths.BackupsDir)
            ? Directory.EnumerateFiles(_appPaths.BackupsDir, "*.json")
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        var items = backupFiles
            .Select(path =>
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (!DateTime.TryParseExact(fileName, "yyyyMMdd_HHmmssfff", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
                {
                    parsed = File.GetLastWriteTimeUtc(path);
                }

                return new BackupItemViewModel(path, parsed);
            })
            .ToList();

        Backups = new ObservableCollection<BackupItemViewModel>(items);
        if (Backups.Count > 0)
        {
            SelectedBackup = Backups[0];
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (string.IsNullOrWhiteSpace(ActiveWorkshopPath) || ActiveWorkshopPath == "Not detected")
        {
            StatusMessage = "Workshop path not set";
            return;
        }

        await _backupService.CreateSnapshotAsync(ActiveWorkshopPath);
        await _logger.LogAsync("Created workshop snapshot");
        await ReloadBackupsAsync();
        StatusMessage = "Backup created";
    }

    [RelayCommand]
    private async Task RestoreSelectedBackupAsync()
    {
        if (SelectedBackup is null)
        {
            StatusMessage = "No backup selected";
            return;
        }

        try
        {
            await using var stream = File.OpenRead(SelectedBackup.FilePath);
            var snapshot = await JsonSerializer.DeserializeAsync<WorkshopSnapshot>(stream);
            if (snapshot is null)
            {
                StatusMessage = "Failed to read backup";
                return;
            }

            var requiredOrderedIds = snapshot.Folders
                .Select(name => WorkshopScanner.TryGetWorkshopId(name, out var id, out var enabled) ? (id, enabled) : default)
                .Where(tuple => !string.IsNullOrWhiteSpace(tuple.id))
                .Where(tuple => tuple.enabled)
                .Select(tuple => tuple.id)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            await ApplyEnabledSetAsync(requiredOrderedIds.ToHashSet(StringComparer.Ordinal), requiredOrderedIds, "backup restore");
            StatusMessage = $"Restored backup {SelectedBackup.FileName}";
        }
        catch
        {
            StatusMessage = "Failed to restore selected backup";
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedBackupAsync(IList? selectedItems)
    {
        var selectedBackups = selectedItems?
            .OfType<BackupItemViewModel>()
            .Distinct()
            .ToList() ?? new List<BackupItemViewModel>();

        if (selectedBackups.Count == 0 && SelectedBackup is not null)
        {
            selectedBackups.Add(SelectedBackup);
        }

        if (selectedBackups.Count == 0)
        {
            StatusMessage = "No backup selected";
            return;
        }

        var deletedCount = 0;
        foreach (var backup in selectedBackups)
        {
            try
            {
                if (File.Exists(backup.FilePath))
                {
                    File.Delete(backup.FilePath);
                    deletedCount++;
                }
            }
            catch
            {
                // Continue deleting others.
            }
        }

        await _logger.LogAsync($"Deleted {deletedCount}/{selectedBackups.Count} selected backups");
        await ReloadBackupsAsync();
        StatusMessage = deletedCount == selectedBackups.Count
            ? $"Deleted {deletedCount} backups"
            : $"Deleted {deletedCount}/{selectedBackups.Count} backups";
    }

    [RelayCommand]
    private async Task ReloadLogsAsync()
    {
        var files = Directory.Exists(_appPaths.LogsDir)
            ? Directory.EnumerateFiles(_appPaths.LogsDir, "app-*.log")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        if (files.Count == 0)
        {
            files = File.Exists(_appPaths.LogFile)
                ? new List<string> { _appPaths.LogFile }
                : new List<string>();
        }

        var entries = new List<LogEntryViewModel>();
        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                continue;
            }

            var lines = await File.ReadAllLinesAsync(file);
            entries.AddRange(lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => new LogEntryViewModel(line, file)));
        }

        _rawLogs = entries
            .OrderBy(entry => entry.Timestamp ?? DateTime.MinValue)
            .ThenBy(entry => entry.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ApplyLogFilter();
    }

    [RelayCommand]
    private async Task DeleteSelectedLogAsync(IList? selectedItems)
    {
        var selectedLogs = selectedItems?
            .OfType<LogEntryViewModel>()
            .ToList() ?? new List<LogEntryViewModel>();

        if (selectedLogs.Count == 0 && SelectedLogEntry is not null)
        {
            selectedLogs.Add(SelectedLogEntry);
        }

        if (selectedLogs.Count == 0)
        {
            StatusMessage = "No log entry selected";
            return;
        }

        try
        {
            var removedCount = 0;
            var grouped = selectedLogs
                .Where(log => !string.IsNullOrWhiteSpace(log.SourceFile))
                .GroupBy(log => log.SourceFile, StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouped)
            {
                if (!File.Exists(group.Key))
                {
                    continue;
                }

                var lines = (await File.ReadAllLinesAsync(group.Key)).ToList();
                foreach (var logEntry in group)
                {
                    if (lines.Remove(logEntry.Text))
                    {
                        removedCount++;
                    }
                }

                await File.WriteAllLinesAsync(group.Key, lines);
            }

            await ReloadLogsAsync();
            StatusMessage = removedCount == selectedLogs.Count
                ? $"Deleted {removedCount} log entries"
                : $"Deleted {removedCount}/{selectedLogs.Count} log entries";
        }
        catch
        {
            StatusMessage = "Failed to delete selected log entries";
        }
    }

    [RelayCommand]
    private async Task DeleteAllLogsAsync()
    {
        try
        {
            if (Directory.Exists(_appPaths.LogsDir))
            {
                foreach (var file in Directory.EnumerateFiles(_appPaths.LogsDir, "app-*.log"))
                {
                    await File.WriteAllTextAsync(file, string.Empty);
                }
            }

            await ReloadLogsAsync();
            StatusMessage = "Deleted all logs";
        }
        catch
        {
            StatusMessage = "Failed to delete all logs";
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            if (!Directory.Exists(_appPaths.LogsDir))
            {
                Directory.CreateDirectory(_appPaths.LogsDir);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_appPaths.LogsDir}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            StatusMessage = "Failed to open log folder";
        }
    }

    private void ApplyLogFilter()
    {
        var filtered = _rawLogs
            .Where(log =>
                (ShowInfoLogs && log.Level == AppLogLevel.Info) ||
                (ShowWarningLogs && log.Level == AppLogLevel.Warning) ||
                (ShowErrorLogs && log.Level == AppLogLevel.Error))
            .ToList();

        AllLogs = new ObservableCollection<LogEntryViewModel>(filtered);
        RecentLogs = new ObservableCollection<LogEntryViewModel>(filtered.TakeLast(8));
        SelectedLogEntry = AllLogs.Count > 0 ? AllLogs[^1] : null;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task PlayAsync()
    {
        await PlayModdedAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task PlayModdedAsync()
    {
        if (IsLaunchingGame)
        {
            RequestLaunchCancel("modded");
            return;
        }

        if (IsVtolRunning)
        {
            await StopVtolVrAsync();
            return;
        }

        var launchToken = BeginLaunch();
        IsLaunchingGame = true;
        StatusMessage = "Launching modded...";
        await Task.Yield();

        try
        {
            ProfileItemViewModel? profileToUse = null;

            if (CurrentPageViewModel is ProfileDetailsPageViewModel detailsPage)
            {
                profileToUse = detailsPage.Profile;
            }

            if (profileToUse is null)
            {
                profileToUse = await PickProfileAsync();
                if (IsLaunchCanceled(launchToken, "Modded launch canceled"))
                {
                    return;
                }
            }

            if (profileToUse is null)
            {
                StatusMessage = "Modded launch canceled";
                return;
            }

            SelectedProfile = profileToUse;
            await ApplySelectedProfileAsync(launchToken);
            if (IsLaunchCanceled(launchToken, "Modded launch canceled"))
            {
                return;
            }

            await EnsureDoorstopEnabledAsync(true, "modded launch");
            if (IsLaunchCanceled(launchToken, "Modded launch canceled"))
            {
                return;
            }

            LaunchVtolVr(doorstopEnabled: true);
            StartVtolExitCleanupWatcher();
            await TryCloseModManagerIfRunningAsync();
            await _logger.LogAsync($"Launched VTOL VR modded with profile '{profileToUse.Name}'");
            await ReloadLogsAsync();
        }
        catch (OperationCanceledException) when (launchToken.IsCancellationRequested)
        {
            StatusMessage = "Modded launch canceled";
        }
        finally
        {
            EndLaunch(launchToken);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task PlayVanillaAsync()
    {
        if (IsLaunchingGame)
        {
            RequestLaunchCancel("vanilla");
            return;
        }

        if (IsVtolRunning)
        {
            await StopVtolVrAsync();
            return;
        }

        var launchToken = BeginLaunch();
        IsLaunchingGame = true;
        StatusMessage = "Launching vanilla...";
        await Task.Yield();

        try
        {
            await ApplyEnabledSetAsync(
                new HashSet<string>(StringComparer.Ordinal),
                Array.Empty<string>(),
                "vanilla launch",
                launchToken);
            if (IsLaunchCanceled(launchToken, "Vanilla launch canceled"))
            {
                return;
            }

            await EnsureDoorstopEnabledAsync(false, "vanilla launch");
            if (IsLaunchCanceled(launchToken, "Vanilla launch canceled"))
            {
                return;
            }

            LaunchVtolVr(doorstopEnabled: false);
            StartVtolExitCleanupWatcher();
            await _logger.LogAsync("Launched VTOL VR vanilla");
            await ReloadLogsAsync();
        }
        catch (OperationCanceledException) when (launchToken.IsCancellationRequested)
        {
            StatusMessage = "Vanilla launch canceled";
        }
        finally
        {
            EndLaunch(launchToken);
        }
    }

    private void LaunchVtolVr(bool doorstopEnabled)
    {
        var vrRuntimeArg = GetVrRuntimeLaunchArgument();
        var arguments = $"-applaunch 667970 --doorstop-enabled {(doorstopEnabled ? "true" : "false")}";
        if (!string.IsNullOrWhiteSpace(vrRuntimeArg))
        {
            arguments = $"{arguments} {vrRuntimeArg}";
        }

        var steamExePath = ResolveSteamExePath();
        if (!string.IsNullOrWhiteSpace(steamExePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = steamExePath,
                Arguments = arguments,
                UseShellExecute = false
            });

            TryLaunchSteamQueries();
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "steam://run/667970",
            UseShellExecute = true
        });

        TryLaunchSteamQueries();
    }

    private void StartVtolRunningMonitor()
    {
        _vtolRunningMonitorCts?.Cancel();
        _vtolRunningMonitorCts?.Dispose();
        _vtolRunningMonitorCts = new CancellationTokenSource();
        _ = MonitorVtolRunningAsync(_vtolRunningMonitorCts.Token);
    }

    private async Task MonitorVtolRunningAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var running = IsVtolProcessRunning();
                if (running != IsVtolRunning)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => IsVtolRunning = running);
                }

                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // View model is shutting down.
        }
    }

    private static bool IsVtolProcessRunning()
    {
        var processes = Process.GetProcessesByName("VTOLVR");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private async Task StopVtolVrAsync()
    {
        var processes = Process.GetProcessesByName("VTOLVR");
        if (processes.Length == 0)
        {
            StatusMessage = "VTOL VR is not running";
            IsVtolRunning = false;
            return;
        }

        var stoppedCount = 0;
        foreach (var process in processes)
        {
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                if (process.CloseMainWindow())
                {
                    process.WaitForExit(2000);
                }

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }

                stoppedCount++;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync($"Failed to stop VTOLVR process (PID {process.Id}): {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        IsVtolRunning = IsVtolProcessRunning();
        StatusMessage = stoppedCount > 0 ? "Stopped VTOL VR" : "Failed to stop VTOL VR";
        await _logger.LogAsync(stoppedCount > 0
            ? $"Stopped VTOL VR process(es): {stoppedCount}"
            : "Failed to stop VTOL VR process");
        await ReloadLogsAsync();
    }

    private void StartVtolExitCleanupWatcher()
    {
        _vtolExitCleanupCts?.Cancel();
        _vtolExitCleanupCts?.Dispose();
        _vtolExitCleanupCts = new CancellationTokenSource();
        _ = WatchForVtolExitAndRestoreModsAsync(_vtolExitCleanupCts.Token);
    }

    private async Task WatchForVtolExitAndRestoreModsAsync(CancellationToken cancellationToken)
    {
        try
        {
            const int maxStartWaitAttempts = 120; // ~2 minutes
            var seenRunning = false;

            for (var attempt = 0; attempt < maxStartWaitAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var probe = Process.GetProcessesByName("VTOLVR");
                try
                {
                    if (probe.Length > 0)
                    {
                        seenRunning = true;
                        break;
                    }
                }
                finally
                {
                    foreach (var process in probe)
                    {
                        process.Dispose();
                    }
                }

                await Task.Delay(1000, cancellationToken);
            }

            if (!seenRunning)
            {
                await _logger.LogAsync("VTOL exit watcher: VTOLVR process not detected after launch; skipping # prefix cleanup.");
                return;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var active = Process.GetProcessesByName("VTOLVR");
                try
                {
                    if (active.Length == 0)
                    {
                        break;
                    }
                }
                finally
                {
                    foreach (var process in active)
                    {
                        process.Dispose();
                    }
                }

                await Task.Delay(1500, cancellationToken);
            }

            await RestoreDisabledModFoldersAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // View model is shutting down or a newer launch replaced this watcher.
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"VTOL exit watcher failed: {ex.Message}");
        }
    }

    private async Task RestoreDisabledModFoldersAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ActiveWorkshopPath) ||
            string.Equals(ActiveWorkshopPath, "Not detected", StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(ActiveWorkshopPath))
        {
            await _logger.LogAsync("VTOL exit cleanup skipped: active workshop path is unavailable.");
            return;
        }

        var folderNames = await Task.Run(
            () => Directory.EnumerateDirectories(ActiveWorkshopPath).Select(Path.GetFileName).ToList(),
            cancellationToken);

        var parsedMods = new List<WorkshopMod>(folderNames.Count);
        foreach (var folderName in folderNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(folderName))
            {
                continue;
            }

            if (!WorkshopScanner.TryGetWorkshopId(folderName, out var workshopId, out var isEnabled))
            {
                continue;
            }

            parsedMods.Add(new WorkshopMod
            {
                WorkshopId = workshopId,
                FolderName = folderName,
                FullPath = Path.Combine(ActiveWorkshopPath, folderName),
                IsEnabled = isEnabled,
                DisplayName = $"Mod {workshopId}"
            });
        }

        if (parsedMods.Count == 0)
        {
            return;
        }

        var allIds = parsedMods
            .Select(mod => mod.WorkshopId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (allIds.Count == 0)
        {
            return;
        }

        var renamedCount = await _renameEngine.ApplyEnabledSetAsync(
            ActiveWorkshopPath,
            parsedMods,
            allIds,
            disableUnselectedMods: false,
            logAsync: (message, token) => _logger.LogAsync(message),
            cancellationToken: cancellationToken);

        if (renamedCount > 0)
        {
            await _logger.LogAsync($"VTOL exit cleanup: removed # prefix from {renamedCount} mod folders.");
        }
    }

    private void TryLaunchSteamQueries()
    {
        var steamQueriesExePath = ResolveSteamQueriesExePath();
        if (string.IsNullOrWhiteSpace(steamQueriesExePath) || !File.Exists(steamQueriesExePath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = steamQueriesExePath,
                WorkingDirectory = Path.GetDirectoryName(steamQueriesExePath) ?? string.Empty,
                UseShellExecute = false
            });
        }
        catch
        {
            // Optional helper launch should never block game launch.
        }
    }

    private async Task TryCloseModManagerIfRunningAsync()
    {
        var modManagerExePath = ResolveModManagerExePath();
        if (string.IsNullOrWhiteSpace(modManagerExePath))
        {
            return;
        }

        var expectedPath = Path.GetFullPath(modManagerExePath);
        var candidates = Process.GetProcessesByName("Mod Manager");
        if (candidates.Length == 0)
        {
            return;
        }

        var warningPrefix = "[WARNING]";
        Console.WriteLine($"{warningPrefix} Mod Manager is running after modded launch, closing it.");
        await _logger.LogAsync("WARNING: Mod Manager is running after modded launch, closing it.");

        foreach (var process in candidates)
        {
            try
            {
                var processPath = string.Empty;
                try
                {
                    processPath = process.MainModule?.FileName ?? string.Empty;
                }
                catch
                {
                    // Ignore path probe issues and continue with process name match.
                }

                if (!string.IsNullOrWhiteSpace(processPath))
                {
                    var normalizedProcessPath = Path.GetFullPath(processPath);
                    if (!string.Equals(normalizedProcessPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                if (process.HasExited)
                {
                    continue;
                }

                if (process.CloseMainWindow())
                {
                    process.WaitForExit(1500);
                }

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(1500);
                }

                Console.WriteLine($"{warningPrefix} Closed Mod Manager (PID {process.Id}).");
                await _logger.LogAsync($"WARNING: Closed Mod Manager (PID {process.Id}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{warningPrefix} Failed to close Mod Manager (PID {process.Id}): {ex.Message}");
                await _logger.LogAsync($"WARNING: Failed to close Mod Manager (PID {process.Id}): {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private string ResolveModManagerExePath()
    {
        var candidates = new List<string>();

        var fromDoorstopConfig = ResolveDoorstopConfigPath();
        if (!string.IsNullOrWhiteSpace(fromDoorstopConfig))
        {
            try
            {
                var vtolDir = Path.GetDirectoryName(fromDoorstopConfig);
                if (!string.IsNullOrWhiteSpace(vtolDir))
                {
                    candidates.Add(Path.Combine(vtolDir, "@Mod Loader", "Mod Manager", "Mod Manager.exe"));
                }
            }
            catch
            {
                // Fall through to default candidates.
            }
        }

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "steamapps",
            "common",
            "VTOL VR",
            "@Mod Loader",
            "Mod Manager",
            "Mod Manager.exe"));
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Steam",
            "steamapps",
            "common",
            "VTOL VR",
            "@Mod Loader",
            "Mod Manager",
            "Mod Manager.exe"));

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private string ResolveSteamQueriesExePath()
    {
        var candidates = new List<string>();

        var fromDoorstopConfig = ResolveDoorstopConfigPath();
        if (!string.IsNullOrWhiteSpace(fromDoorstopConfig))
        {
            try
            {
                var vtolDir = Path.GetDirectoryName(fromDoorstopConfig);
                if (!string.IsNullOrWhiteSpace(vtolDir))
                {
                    candidates.Add(Path.Combine(vtolDir, "@Mod Loader", "SteamQueries", "SteamQueries.exe"));
                }
            }
            catch
            {
                // Fall through to default candidates.
            }
        }

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "steamapps",
            "common",
            "VTOL VR",
            "@Mod Loader",
            "SteamQueries",
            "SteamQueries.exe"));
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Steam",
            "steamapps",
            "common",
            "VTOL VR",
            "@Mod Loader",
            "SteamQueries",
            "SteamQueries.exe"));

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private string ResolveSteamExePath()
    {
        var candidates = new List<string>();

        var fromDoorstopConfig = ResolveDoorstopConfigPath();
        if (!string.IsNullOrWhiteSpace(fromDoorstopConfig))
        {
            try
            {
                var vtolDir = Path.GetDirectoryName(fromDoorstopConfig);
                var commonDir = vtolDir is null ? null : Path.GetDirectoryName(vtolDir);
                var steamAppsDir = commonDir is null ? null : Path.GetDirectoryName(commonDir);
                var steamRoot = steamAppsDir is null ? null : Path.GetDirectoryName(steamAppsDir);
                if (!string.IsNullOrWhiteSpace(steamRoot))
                {
                    candidates.Add(Path.Combine(steamRoot, "steam.exe"));
                }
            }
            catch
            {
                // Fall through to other candidates.
            }
        }

        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steam.exe"));

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private async Task EnsureDoorstopEnabledAsync(bool enabled, string context)
    {
        if (!TrySetDoorstopEnabled(enabled, out var configPath, out var errorMessage))
        {
            StatusMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? "Failed to update doorstop_config.ini"
                : $"Doorstop update failed: {errorMessage}";
            await _logger.LogAsync(
                $"Doorstop set to {(enabled ? "true" : "false")} failed ({context}). path='{configPath}' error='{errorMessage}'");
            return;
        }

        // Intentionally avoid logging successful doorstop toggles to keep launch logs clean.
    }

    private bool TrySetDoorstopEnabled(bool enabled, out string configPath, out string errorMessage)
    {
        configPath = ResolveDoorstopConfigPath();
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(configPath))
        {
            errorMessage = "doorstop_config.ini path not found";
            return false;
        }

        try
        {
            if (!File.Exists(configPath))
            {
                errorMessage = "doorstop_config.ini does not exist";
                return false;
            }

            var lines = File.ReadAllLines(configPath).ToList();
            var wrote = false;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].TrimStart().StartsWith("enabled=", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"enabled={(enabled ? "true" : "false")}";
                    wrote = true;
                    break;
                }
            }

            if (!wrote)
            {
                lines.Add($"enabled={(enabled ? "true" : "false")}");
            }

            File.WriteAllLines(configPath, lines);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private string ResolveDoorstopConfigPath()
    {
        if (!string.IsNullOrWhiteSpace(ActiveWorkshopPath) && ActiveWorkshopPath != "Not detected")
        {
            try
            {
                var normalized = ActiveWorkshopPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var appIdDir = Path.GetFileName(normalized);
                var contentDir = Path.GetDirectoryName(normalized);
                if (string.Equals(appIdDir, "3018410", StringComparison.Ordinal) &&
                    contentDir is not null &&
                    string.Equals(Path.GetFileName(contentDir), "content", StringComparison.OrdinalIgnoreCase))
                {
                    var workshopDir = Path.GetDirectoryName(contentDir);
                    if (workshopDir is not null &&
                        string.Equals(Path.GetFileName(workshopDir), "workshop", StringComparison.OrdinalIgnoreCase))
                    {
                        var steamAppsDir = Path.GetDirectoryName(workshopDir);
                        if (steamAppsDir is not null)
                        {
                            var fromActive = Path.Combine(steamAppsDir, "common", "VTOL VR", "doorstop_config.ini");
                            if (File.Exists(fromActive))
                            {
                                return fromActive;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fall through to default candidates.
            }
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "VTOL VR", "doorstop_config.ini"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "VTOL VR", "doorstop_config.ini")
        };

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private async Task<ProfileItemViewModel?> PickProfileAsync()
    {
        if (Profiles.Count == 0)
        {
            StatusMessage = "No profiles available";
            return null;
        }

        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return Profiles.FirstOrDefault();
        }

        var dialog = new ProfilePickerWindow(Profiles.ToList(), SelectedProfile);
        return await dialog.ShowDialog<ProfileItemViewModel?>(desktop.MainWindow);
    }

    private void UpdateSelectedModDetailFields(ModItemViewModel? modViewModel)
    {
        if (modViewModel is null)
        {
            _selectedModAuthor = "n/a";
            _selectedModLastUpdated = "n/a";
            _selectedModLocalVersion = "n/a";
            _selectedModRemoteVersion = "n/a";
            _selectedModSize = "n/a";
            _selectedModStatus = "n/a";
            _selectedModHasUpdate = false;
            NotifySelectedModDetailPropertiesChanged();
            return;
        }

        var source = modViewModel.Source;
        _selectedModAuthor = string.IsNullOrWhiteSpace(source.Author) ? "n/a" : source.Author!;
        _selectedModLastUpdated = source.LastUpdatedUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "n/a";
        _selectedModLocalVersion = string.IsNullOrWhiteSpace(source.LocalVersion) ? "n/a" : source.LocalVersion!;
        _selectedModRemoteVersion = string.IsNullOrWhiteSpace(source.RemoteVersion) ? "n/a" : source.RemoteVersion!;
        _selectedModSize = "calculating...";
        _ = UpdateSelectedModSizeAsync(source.WorkshopId, source.FullPath);

        var isMissing = string.IsNullOrWhiteSpace(source.FullPath) || !Directory.Exists(source.FullPath);
        if (isMissing)
        {
            _selectedModStatus = "Missing";
            _selectedModHasUpdate = false;
            NotifySelectedModDetailPropertiesChanged();
            return;
        }

        var hasLocalVersion = TryParseVersion(source.LocalVersion, out var localVersion);
        var hasRemoteVersion = TryParseVersion(source.RemoteVersion, out var remoteVersion);
        var hasComparableVersions = hasLocalVersion && hasRemoteVersion;

        if (hasComparableVersions && remoteVersion > localVersion)
        {
            _selectedModStatus = "Update Available";
            _selectedModHasUpdate = true;
        }
        else if (hasComparableVersions)
        {
            _selectedModStatus = "Up-to-date";
            _selectedModHasUpdate = false;
        }
        else
        {
            _selectedModStatus = "Installed";
            _selectedModHasUpdate = false;
        }

        NotifySelectedModDetailPropertiesChanged();
    }

    private async Task UpdateSelectedModSizeAsync(string workshopId, string fullPath)
    {
        long? size = null;
        try
        {
            size = await Task.Run(() => _directorySizeCache.GetDirectorySizeBytes(fullPath));
        }
        catch
        {
            size = null;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (SelectedMod is null || !string.Equals(SelectedMod.WorkshopId, workshopId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedModSize = DirectorySizeCacheService.FormatBytes(size);
            OnPropertyChanged(nameof(SelectedModSize));
        });
    }

    private void NotifySelectedModDetailPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedModAuthor));
        OnPropertyChanged(nameof(SelectedModLastUpdated));
        OnPropertyChanged(nameof(SelectedModLocalVersion));
        OnPropertyChanged(nameof(SelectedModRemoteVersion));
        OnPropertyChanged(nameof(SelectedModSize));
        OnPropertyChanged(nameof(SelectedModStatus));
        OnPropertyChanged(nameof(SelectedModHasUpdate));
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            version = new Version(0, 0);
            return false;
        }

        if (Version.TryParse(value.Trim().TrimStart('v', 'V'), out var parsed) && parsed is not null)
        {
            version = parsed;
            return true;
        }

        version = new Version(0, 0);
        return false;
    }
}

