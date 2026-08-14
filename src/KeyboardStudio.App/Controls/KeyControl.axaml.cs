using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace KeyboardStudio.App.Controls;

public sealed partial class KeyControl : UserControl
{
    public static readonly StyledProperty<string> KeyIdProperty =
        AvaloniaProperty.Register<KeyControl, string>(nameof(KeyId), string.Empty);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<KeyControl, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> HintProperty =
        AvaloniaProperty.Register<KeyControl, string>(nameof(Hint), string.Empty);

    public static readonly StyledProperty<bool> ShowHintProperty =
        AvaloniaProperty.Register<KeyControl, bool>(nameof(ShowHint));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<KeyControl, bool>(nameof(IsSelected));

    public static readonly StyledProperty<bool> IsUnmappedProperty =
        AvaloniaProperty.Register<KeyControl, bool>(nameof(IsUnmapped));

    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<KeyControl, bool>(nameof(HasError));

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<KeyControl, ICommand?>(nameof(SelectCommand));

    public KeyControl()
    {
        InitializeComponent();
    }

    public string KeyId
    {
        get => GetValue(KeyIdProperty);
        set => SetValue(KeyIdProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public bool ShowHint
    {
        get => GetValue(ShowHintProperty);
        set => SetValue(ShowHintProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsUnmapped
    {
        get => GetValue(IsUnmappedProperty);
        set => SetValue(IsUnmappedProperty, value);
    }

    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }
}
