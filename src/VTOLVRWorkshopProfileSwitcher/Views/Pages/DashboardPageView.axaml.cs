using Avalonia.Controls;
using Avalonia.Input;
using VTOLVRWorkshopProfileSwitcher.ViewModels;
using VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

namespace VTOLVRWorkshopProfileSwitcher.Views.Pages;

public partial class DashboardPageView : UserControl
{
    public DashboardPageView()
    {
        InitializeComponent();
    }

    private void OnPlayModdedButtonPointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is DashboardPageViewModel vm)
        {
            vm.Shell.SetLaunchButtonHovered(MainWindowViewModel.LaunchHoverTargetModded, true);
        }
    }

    private void OnPlayModdedButtonPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is DashboardPageViewModel vm)
        {
            vm.Shell.SetLaunchButtonHovered(MainWindowViewModel.LaunchHoverTargetModded, false);
        }
    }

    private void OnPlayVanillaButtonPointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is DashboardPageViewModel vm)
        {
            vm.Shell.SetLaunchButtonHovered(MainWindowViewModel.LaunchHoverTargetVanilla, true);
        }
    }

    private void OnPlayVanillaButtonPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is DashboardPageViewModel vm)
        {
            vm.Shell.SetLaunchButtonHovered(MainWindowViewModel.LaunchHoverTargetVanilla, false);
        }
    }
}
