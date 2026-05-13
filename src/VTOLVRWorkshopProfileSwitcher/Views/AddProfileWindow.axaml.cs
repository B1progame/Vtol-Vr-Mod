using Avalonia.Controls;
using VTOLVRWorkshopProfileSwitcher.ViewModels;

namespace VTOLVRWorkshopProfileSwitcher.Views;

public partial class AddProfileWindow : Window
{
    public AddProfileWindow()
    {
        InitializeComponent();
        DataContext = new AddProfileDialogViewModel();
    }

    private void OnCreateClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not AddProfileDialogViewModel vm)
        {
            Close(null);
            return;
        }

        Close(new AddProfileDialogResult(
            vm.ProfileName,
            vm.Notes,
            vm.ActivateAllMods,
            vm.SelectedProfileIcon.IconName));
    }

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
