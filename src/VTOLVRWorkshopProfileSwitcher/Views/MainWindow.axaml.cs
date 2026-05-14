using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using VTOLVRWorkshopProfileSwitcher.ViewModels;

namespace VTOLVRWorkshopProfileSwitcher.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _subscribedViewModel;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _showLauncherMenuItem;
    private NativeMenuItem? _playModdedMenuItem;
    private NativeMenuItem? _playVanillaMenuItem;
    private NativeMenuItem? _stopVtolMenuItem;
    private NativeMenuItem? _settingsMenuItem;
    private NativeMenuItem? _quitMenuItem;
    private bool _hideToTrayAfterLaunchPending;
    private bool _restoreWindowWhenGameStops;
    private bool _quitAfterGameStops;
    private bool _isQuitting;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        EnsureTrayIcon();

        Dispatcher.UIThread.Post(() =>
        {
            Opacity = 1;
        }, DispatcherPriority.Background);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_isQuitting &&
            DataContext is MainWindowViewModel vm &&
            vm.IsVtolRunning)
        {
            e.Cancel = true;
            _restoreWindowWhenGameStops = true;
            HideToTray();
            return;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnClosing(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel = null;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            _subscribedViewModel = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            ApplyDesignPreset(vm.SelectedDesign);
            EnsureTrayIcon();
            if (_playModdedMenuItem is not null)
            {
                _playModdedMenuItem.Command = vm.PlayModdedCommand;
            }

            if (_playVanillaMenuItem is not null)
            {
                _playVanillaMenuItem.Command = vm.PlayVanillaCommand;
            }

            if (_stopVtolMenuItem is not null)
            {
                _stopVtolMenuItem.Command = vm.StopVtolCommand;
            }

            UpdateTrayMenu(vm);
            UpdateTrayToolTip(vm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.SelectedDesign))
        {
            ApplyDesignPreset(vm.SelectedDesign);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.RequestTrayHideAfterLaunch) &&
            vm.RequestTrayHideAfterLaunch)
        {
            _hideToTrayAfterLaunchPending = true;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsVtolRunning))
        {
            if (vm.IsVtolRunning && _hideToTrayAfterLaunchPending)
            {
                _hideToTrayAfterLaunchPending = false;
                _restoreWindowWhenGameStops = true;
                vm.ClearTrayHideAfterLaunchRequest();
                HideToTray();
            }
            else if (!vm.IsVtolRunning)
            {
                _hideToTrayAfterLaunchPending = false;
                vm.ClearTrayHideAfterLaunchRequest();

                if (_quitAfterGameStops)
                {
                    _quitAfterGameStops = false;
                    _isQuitting = true;
                    Close();
                    return;
                }

                if (_restoreWindowWhenGameStops || _trayIcon?.IsVisible == true)
                {
                    _restoreWindowWhenGameStops = false;
                    RestoreFromTray();
                }
            }
        }

        if (e.PropertyName is nameof(MainWindowViewModel.IsLaunchingGame) or
            nameof(MainWindowViewModel.IsVtolRunning))
        {
            UpdateTrayMenu(vm);
            UpdateTrayToolTip(vm);
        }
    }

    private void ApplyDesignPreset(string? design)
    {
        var isBlue = string.Equals(design, "STEEL BLUE", StringComparison.OrdinalIgnoreCase);
        Classes.Set("design-blue", isBlue);
        Classes.Set("design-red", !isBlue);
    }

    private void OnLaunchFlyoutOpened(object? sender, EventArgs e)
    {
        LaunchArrowButton.Classes.Set("open", true);
        LaunchArrowGlyph.Classes.Set("open", true);
    }

    private void OnLaunchFlyoutClosed(object? sender, EventArgs e)
    {
        LaunchArrowButton.Classes.Set("open", false);
        LaunchArrowGlyph.Classes.Set("open", false);
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        _showLauncherMenuItem = new NativeMenuItem { Header = "Show Launcher" };
        _showLauncherMenuItem.Click += (_, _) => RestoreFromTray();

        _playModdedMenuItem = new NativeMenuItem { Header = "Play Modded" };
        _playVanillaMenuItem = new NativeMenuItem { Header = "Play Vanilla" };
        _stopVtolMenuItem = new NativeMenuItem { Header = "Stop VTOL VR" };
        _settingsMenuItem = new NativeMenuItem { Header = "Open Settings" };
        _settingsMenuItem.Click += (_, _) => OpenSettingsFromTray();
        _quitMenuItem = new NativeMenuItem { Header = "Quit Launcher" };
        _quitMenuItem.Click += (_, _) => QuitFromTray();

        var menu = new NativeMenu();
        menu.Items.Add(_showLauncherMenuItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_playModdedMenuItem);
        menu.Items.Add(_playVanillaMenuItem);
        menu.Items.Add(_stopVtolMenuItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_settingsMenuItem);
        menu.Items.Add(_quitMenuItem);

        _trayIcon = new TrayIcon
        {
            Icon = LoadTrayIcon(),
            ToolTipText = "VTOL VR Switcher",
            Menu = menu,
            IsVisible = false
        };
        _trayIcon.Clicked += (_, _) => RestoreFromTray();

        if (DataContext is MainWindowViewModel vm)
        {
            _playModdedMenuItem.Command = vm.PlayModdedCommand;
            _playVanillaMenuItem.Command = vm.PlayVanillaCommand;
            _stopVtolMenuItem.Command = vm.StopVtolCommand;
            UpdateTrayMenu(vm);
            UpdateTrayToolTip(vm);
        }
    }

    private void HideToTray()
    {
        EnsureTrayIcon();
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.IsVisible = true;
        ShowInTaskbar = false;
        Hide();
    }

    private void RestoreFromTray()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = false;
        }

        if (!IsVisible)
        {
            Show();
        }

        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OpenSettingsFromTray()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            RestoreFromTray();
            return;
        }

        vm.NavigateSettingsCommand.Execute(null);
        RestoreFromTray();
    }

    private void QuitFromTray()
    {
        if (DataContext is not MainWindowViewModel vm || !vm.IsVtolRunning)
        {
            _isQuitting = true;
            Close();
            return;
        }

        _quitAfterGameStops = true;
        UpdateTrayMenu(vm);
    }

    private void UpdateTrayMenu(MainWindowViewModel vm)
    {
        EnsureTrayIcon();
        if (_showLauncherMenuItem is null ||
            _playModdedMenuItem is null ||
            _playVanillaMenuItem is null ||
            _stopVtolMenuItem is null ||
            _settingsMenuItem is null ||
            _quitMenuItem is null)
        {
            return;
        }

        _showLauncherMenuItem.Header = IsVisible ? "Bring Launcher To Front" : "Show Launcher";
        _playModdedMenuItem.IsVisible = !vm.IsVtolRunning;
        _playVanillaMenuItem.IsVisible = !vm.IsVtolRunning;
        _stopVtolMenuItem.IsVisible = vm.IsVtolRunning;
        _playModdedMenuItem.IsEnabled = !vm.IsLaunchingGame && !vm.IsVtolRunning;
        _playVanillaMenuItem.IsEnabled = !vm.IsLaunchingGame && !vm.IsVtolRunning;
        _stopVtolMenuItem.IsEnabled = vm.IsVtolRunning;
        _settingsMenuItem.IsEnabled = true;
        _quitMenuItem.Header = vm.IsVtolRunning
            ? (_quitAfterGameStops ? "Quit After VTOL VR Closes (Queued)" : "Quit After VTOL VR Closes")
            : "Quit Launcher";
    }

    private void UpdateTrayToolTip(MainWindowViewModel vm)
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.ToolTipText = vm.IsVtolRunning
            ? "VTOL VR Switcher - VTOL VR running"
            : (vm.IsLaunchingGame ? "VTOL VR Switcher - Launching VTOL VR" : "VTOL VR Switcher");
    }

    private static WindowIcon LoadTrayIcon()
    {
        using var iconStream = AssetLoader.Open(new Uri("avares://VTOLVRWorkshopProfileSwitcher/Assets/AppIcon.ico"));
        return new WindowIcon(iconStream);
    }
}
