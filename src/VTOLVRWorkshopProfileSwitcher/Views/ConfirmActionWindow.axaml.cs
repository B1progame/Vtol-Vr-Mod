using Avalonia.Controls;

namespace VTOLVRWorkshopProfileSwitcher.Views;

public partial class ConfirmActionWindow : Window
{
    public ConfirmActionWindow()
    {
        InitializeComponent();
    }

    public ConfirmActionWindow(string title, string message, string confirmText = "Continue", string cancelText = "Cancel")
        : this()
    {
        Title = title;
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;
    }

    private async void OnConfirmClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await CloseAsync(true);
    }

    private async void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await CloseAsync(false);
    }

    private System.Threading.Tasks.Task CloseAsync(bool result)
    {
        Close(result);
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
