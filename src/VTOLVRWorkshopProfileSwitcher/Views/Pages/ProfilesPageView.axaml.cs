using Avalonia.Controls;
using Avalonia.Input;
using VTOLVRWorkshopProfileSwitcher.ViewModels;
using VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

namespace VTOLVRWorkshopProfileSwitcher.Views.Pages;

public partial class ProfilesPageView : UserControl
{
    public ProfilesPageView()
    {
        InitializeComponent();
    }

    private void OnProfileItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: ProfileItemViewModel profile })
        {
            return;
        }

        if (DataContext is not ProfilesPageViewModel pageViewModel)
        {
            return;
        }

        if (pageViewModel.Shell.OpenProfileDetailsCommand.CanExecute(profile))
        {
            pageViewModel.Shell.OpenProfileDetailsCommand.Execute(profile);
        }
    }
}
