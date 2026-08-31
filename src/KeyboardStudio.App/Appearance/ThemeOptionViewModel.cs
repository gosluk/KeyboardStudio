using CommunityToolkit.Mvvm.ComponentModel;

namespace KeyboardStudio.App;

/// <summary>
/// One selectable appearance choice.
/// </summary>
/// <remarks>
/// Selection is reported only on the transition into the selected state. A radio group clears the
/// previous option as part of choosing the next one, and that clearing must not be mistaken for a
/// choice of its own.
/// </remarks>
public sealed class ThemeOptionViewModel : ObservableObject
{
    private readonly Action<ThemeOptionViewModel> _selected;
    private bool _isSelected;

    public ThemeOptionViewModel(
        ApplicationTheme theme,
        string name,
        string description,
        Action<ThemeOptionViewModel> selected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(selected);

        Theme = theme;
        Name = name;
        Description = description;
        _selected = selected;
    }

    public ApplicationTheme Theme { get; }

    /// <summary>The visible label, which is also the accessible name of the choice.</summary>
    public string Name { get; }

    /// <summary>Supporting text, so the choice does not rely on a colour swatch alone.</summary>
    public string Description { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value) || !value)
            {
                return;
            }

            _selected(this);
        }
    }

    internal void SetSelectedWithoutNotifying(bool isSelected)
    {
        if (_isSelected == isSelected)
        {
            return;
        }

        _isSelected = isSelected;
        OnPropertyChanged(nameof(IsSelected));
    }
}
