using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace KeyboardStudio.App.Controls;

public sealed partial class KeyControl : UserControl
{
    public static readonly StyledProperty<string> KeyIdProperty =
        AvaloniaProperty.Register<KeyControl, string>(nameof(KeyId), string.Empty);

    public static readonly StyledProperty<string> KeyNameProperty =
        AvaloniaProperty.Register<KeyControl, string>(nameof(KeyName), string.Empty);

    public static readonly StyledProperty<string> DefaultAssignmentProperty =
        AvaloniaProperty.Register<KeyControl, string>(nameof(DefaultAssignment), string.Empty);

    public static readonly StyledProperty<string> ShiftAssignmentProperty =
        AvaloniaProperty.Register<KeyControl, string>(nameof(ShiftAssignment), string.Empty);

    public static readonly StyledProperty<string> AltGrAssignmentProperty =
        AvaloniaProperty.Register<KeyControl, string>(nameof(AltGrAssignment), string.Empty);

    public static readonly StyledProperty<string> ShiftAltGrAssignmentProperty =
        AvaloniaProperty.Register<KeyControl, string>(nameof(ShiftAltGrAssignment), string.Empty);

    public static readonly StyledProperty<bool> IsDefaultActiveProperty =
        AvaloniaProperty.Register<KeyControl, bool>(nameof(IsDefaultActive));

    public static readonly StyledProperty<bool> IsShiftActiveProperty =
        AvaloniaProperty.Register<KeyControl, bool>(nameof(IsShiftActive));

    public static readonly StyledProperty<bool> IsAltGrActiveProperty =
        AvaloniaProperty.Register<KeyControl, bool>(nameof(IsAltGrActive));

    public static readonly StyledProperty<bool> IsShiftAltGrActiveProperty =
        AvaloniaProperty.Register<KeyControl, bool>(nameof(IsShiftAltGrActive));

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

    public string KeyName
    {
        get => GetValue(KeyNameProperty);
        set => SetValue(KeyNameProperty, value);
    }

    public string DefaultAssignment
    {
        get => GetValue(DefaultAssignmentProperty);
        set => SetValue(DefaultAssignmentProperty, value);
    }

    public string ShiftAssignment
    {
        get => GetValue(ShiftAssignmentProperty);
        set => SetValue(ShiftAssignmentProperty, value);
    }

    public string AltGrAssignment
    {
        get => GetValue(AltGrAssignmentProperty);
        set => SetValue(AltGrAssignmentProperty, value);
    }

    public string ShiftAltGrAssignment
    {
        get => GetValue(ShiftAltGrAssignmentProperty);
        set => SetValue(ShiftAltGrAssignmentProperty, value);
    }

    public bool IsDefaultActive
    {
        get => GetValue(IsDefaultActiveProperty);
        set => SetValue(IsDefaultActiveProperty, value);
    }

    public bool IsShiftActive
    {
        get => GetValue(IsShiftActiveProperty);
        set => SetValue(IsShiftActiveProperty, value);
    }

    public bool IsAltGrActive
    {
        get => GetValue(IsAltGrActiveProperty);
        set => SetValue(IsAltGrActiveProperty, value);
    }

    public bool IsShiftAltGrActive
    {
        get => GetValue(IsShiftAltGrActiveProperty);
        set => SetValue(IsShiftAltGrActiveProperty, value);
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
