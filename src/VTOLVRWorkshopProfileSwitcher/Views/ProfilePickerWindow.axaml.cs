using System.Collections.Generic;
using Avalonia.Controls;
using VTOLVRWorkshopProfileSwitcher.ViewModels;

namespace VTOLVRWorkshopProfileSwitcher.Views;

public partial class ProfilePickerWindow : Window
{
    public ProfilePickerWindow()
    {
        InitializeComponent();
        DataContext = new ProfilePickerDialogViewModel(new List<ProfileItemViewModel>(), null);
    }

    public ProfilePickerWindow(IReadOnlyList<ProfileItemViewModel> profiles, ProfileItemViewModel? selectedProfile)
    {
        InitializeComponent();
        DataContext = new ProfilePickerDialogViewModel(profiles, selectedProfile);
    }

    private void OnUseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProfilePickerDialogViewModel vm)
        {
            Close(vm.SelectedProfile);
            return;
        }

        Close(null);
    }

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
