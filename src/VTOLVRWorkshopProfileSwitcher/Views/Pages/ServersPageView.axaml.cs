using Avalonia.Controls;
using Avalonia.Input;
using VTOLVRWorkshopProfileSwitcher.ViewModels;
using VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

namespace VTOLVRWorkshopProfileSwitcher.Views.Pages;

public partial class ServersPageView : UserControl
{
    public ServersPageView()
    {
        InitializeComponent();
    }

    private void OnServerDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: ServerItemViewModel server })
        {
            return;
        }

        if (DataContext is not ServersPageViewModel pageViewModel)
        {
            return;
        }

        if (pageViewModel.Shell.OpenServerDetailsCommand.CanExecute(server))
        {
            pageViewModel.Shell.OpenServerDetailsCommand.Execute(server);
        }
    }
}
