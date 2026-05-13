using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace VTOLVRWorkshopProfileSwitcher.Views.Controls;

public partial class ModCardView : UserControl
{
    public static readonly StyledProperty<Bitmap?> ThumbnailImageProperty =
        AvaloniaProperty.Register<ModCardView, Bitmap?>(nameof(ThumbnailImage));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ModCardView, string>(nameof(Title), "Unknown Mod");

    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<ModCardView, string>(nameof(Subtitle), string.Empty);

    public static readonly StyledProperty<bool> ShowToggleProperty =
        AvaloniaProperty.Register<ModCardView, bool>(nameof(ShowToggle), true);

    public static readonly StyledProperty<bool> ModEnabledProperty =
        AvaloniaProperty.Register<ModCardView, bool>(nameof(ModEnabled), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<ModCardView, string>(nameof(StatusText), string.Empty);

    public static readonly StyledProperty<bool> ShowStatusTextProperty =
        AvaloniaProperty.Register<ModCardView, bool>(nameof(ShowStatusText), false);

    public static readonly StyledProperty<ICommand?> DetailsCommandProperty =
        AvaloniaProperty.Register<ModCardView, ICommand?>(nameof(DetailsCommand));

    public static readonly StyledProperty<object?> DetailsCommandParameterProperty =
        AvaloniaProperty.Register<ModCardView, object?>(nameof(DetailsCommandParameter));

    public static readonly StyledProperty<string> DetailsTextProperty =
        AvaloniaProperty.Register<ModCardView, string>(nameof(DetailsText), "Details");

    public static readonly StyledProperty<bool> OpenDetailsOnCardClickProperty =
        AvaloniaProperty.Register<ModCardView, bool>(nameof(OpenDetailsOnCardClick), false);

    public static readonly StyledProperty<string> SecondaryActionTextProperty =
        AvaloniaProperty.Register<ModCardView, string>(nameof(SecondaryActionText), string.Empty);

    public static readonly StyledProperty<bool> ShowSecondaryActionProperty =
        AvaloniaProperty.Register<ModCardView, bool>(nameof(ShowSecondaryAction), false);

    public static readonly StyledProperty<ICommand?> SecondaryActionCommandProperty =
        AvaloniaProperty.Register<ModCardView, ICommand?>(nameof(SecondaryActionCommand));

    public static readonly StyledProperty<object?> SecondaryActionCommandParameterProperty =
        AvaloniaProperty.Register<ModCardView, object?>(nameof(SecondaryActionCommandParameter));

    public static readonly StyledProperty<string> TertiaryActionTextProperty =
        AvaloniaProperty.Register<ModCardView, string>(nameof(TertiaryActionText), string.Empty);

    public static readonly StyledProperty<bool> ShowTertiaryActionProperty =
        AvaloniaProperty.Register<ModCardView, bool>(nameof(ShowTertiaryAction), false);

    public static readonly StyledProperty<ICommand?> TertiaryActionCommandProperty =
        AvaloniaProperty.Register<ModCardView, ICommand?>(nameof(TertiaryActionCommand));

    public static readonly StyledProperty<object?> TertiaryActionCommandParameterProperty =
        AvaloniaProperty.Register<ModCardView, object?>(nameof(TertiaryActionCommandParameter));

    public ModCardView()
    {
        InitializeComponent();
    }

    public Bitmap? ThumbnailImage
    {
        get => GetValue(ThumbnailImageProperty);
        set => SetValue(ThumbnailImageProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool ShowToggle
    {
        get => GetValue(ShowToggleProperty);
        set => SetValue(ShowToggleProperty, value);
    }

    public bool ModEnabled
    {
        get => GetValue(ModEnabledProperty);
        set => SetValue(ModEnabledProperty, value);
    }

    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public bool ShowStatusText
    {
        get => GetValue(ShowStatusTextProperty);
        set => SetValue(ShowStatusTextProperty, value);
    }

    public ICommand? DetailsCommand
    {
        get => GetValue(DetailsCommandProperty);
        set => SetValue(DetailsCommandProperty, value);
    }

    public object? DetailsCommandParameter
    {
        get => GetValue(DetailsCommandParameterProperty);
        set => SetValue(DetailsCommandParameterProperty, value);
    }

    public string DetailsText
    {
        get => GetValue(DetailsTextProperty);
        set => SetValue(DetailsTextProperty, value);
    }

    public bool OpenDetailsOnCardClick
    {
        get => GetValue(OpenDetailsOnCardClickProperty);
        set => SetValue(OpenDetailsOnCardClickProperty, value);
    }

    public Cursor CardCursor => OpenDetailsOnCardClick
        ? new Cursor(StandardCursorType.Hand)
        : Cursor.Default;

    public string SecondaryActionText
    {
        get => GetValue(SecondaryActionTextProperty);
        set => SetValue(SecondaryActionTextProperty, value);
    }

    public bool ShowSecondaryAction
    {
        get => GetValue(ShowSecondaryActionProperty);
        set => SetValue(ShowSecondaryActionProperty, value);
    }

    public ICommand? SecondaryActionCommand
    {
        get => GetValue(SecondaryActionCommandProperty);
        set => SetValue(SecondaryActionCommandProperty, value);
    }

    public object? SecondaryActionCommandParameter
    {
        get => GetValue(SecondaryActionCommandParameterProperty);
        set => SetValue(SecondaryActionCommandParameterProperty, value);
    }

    public string TertiaryActionText
    {
        get => GetValue(TertiaryActionTextProperty);
        set => SetValue(TertiaryActionTextProperty, value);
    }

    public bool ShowTertiaryAction
    {
        get => GetValue(ShowTertiaryActionProperty);
        set => SetValue(ShowTertiaryActionProperty, value);
    }

    public ICommand? TertiaryActionCommand
    {
        get => GetValue(TertiaryActionCommandProperty);
        set => SetValue(TertiaryActionCommandProperty, value);
    }

    public object? TertiaryActionCommandParameter
    {
        get => GetValue(TertiaryActionCommandParameterProperty);
        set => SetValue(TertiaryActionCommandParameterProperty, value);
    }

    private void OnCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!OpenDetailsOnCardClick || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        if (e.Source is Button or ToggleSwitch)
        {
            return;
        }

        var command = DetailsCommand;
        var parameter = DetailsCommandParameter;
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
            e.Handled = true;
        }
    }
}
