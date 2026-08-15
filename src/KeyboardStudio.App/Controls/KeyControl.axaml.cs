using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

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

    public static readonly StyledProperty<bool> IsIsoEnterProperty =
        AvaloniaProperty.Register<KeyControl, bool>(nameof(IsIsoEnter));

    public static readonly StyledProperty<bool> IsRectangularProperty =
        AvaloniaProperty.Register<KeyControl, bool>(nameof(IsRectangular), true);

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

    public bool IsIsoEnter
    {
        get => GetValue(IsIsoEnterProperty);
        set => SetValue(IsIsoEnterProperty, value);
    }

    public bool IsRectangular
    {
        get => GetValue(IsRectangularProperty);
        set => SetValue(IsRectangularProperty, value);
    }

    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateIsoEnterShape(e.NewSize);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsIsoEnterProperty)
        {
            UpdateIsoEnterShape(Bounds.Size);
        }
    }

    private void UpdateIsoEnterShape(Size size)
    {
        if (!IsIsoEnter || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var width = size.Width;
        var height = size.Height;
        var lowerInset = width * (14.5d / 83d);
        var elbow = height * (54d / 112d);
        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, 0), true);
            context.LineTo(new Point(width, 0));
            context.LineTo(new Point(width, height));
            context.LineTo(new Point(lowerInset, height));
            context.LineTo(new Point(lowerInset, elbow));
            context.LineTo(new Point(0, elbow));
            context.EndFigure(true);
        }

        IsoEnterButton.Clip = geometry;
        IsoEnterOutline.Data = geometry;
    }
}
