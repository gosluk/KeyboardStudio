using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class KeyViewModel : ObservableObject
{
    private bool _hasError;
    private string _hint;
    private bool _isSelected;
    private bool _isUnmapped;
    private string _label;

    public KeyViewModel(
        PhysicalKey key,
        KeyMapping? mapping,
        Action<KeyViewModel> select,
        double unitWidth,
        double unitGap)
    {
        Key = key;
        Mapping = mapping;
        _label = GetPhysicalLabel(key.Id);
        _hint = GetHint(key.Id, mapping);
        _isUnmapped = true;
        SelectCommand = new RelayCommand(() => select(this));
        Left = ToPosition(key.X, unitWidth, unitGap);
        Top = ToPosition(key.Y, unitWidth, unitGap);
        Width = ToLength(key.Width, unitWidth, unitGap);
        Height = ToLength(key.Height, unitWidth, unitGap);
    }

    public PhysicalKey Key { get; }
    public string KeyId => Key.Id;
    public string ScanCode => $"0x{Key.ScanCode:X2}";
    public KeyMapping? Mapping { get; set; }
    public IRelayCommand SelectCommand { get; }
    public double Left { get; }
    public double Top { get; }
    public double Width { get; }
    public double Height { get; }

    public string Hint
    {
        get => _hint;
        private set => SetProperty(ref _hint, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }

    public bool IsUnmapped
    {
        get => _isUnmapped;
        private set => SetProperty(ref _isUnmapped, value);
    }

    public bool HasError
    {
        get => _hasError;
        internal set => SetProperty(ref _hasError, value);
    }

    public string Label
    {
        get => _label;
        private set => SetProperty(ref _label, value);
    }

    public void Refresh(ModifierLayer layer)
    {
        KeyOutput? output = null;
        var hasOutput = Mapping?.Outputs.TryGetValue(layer, out output) == true && output is not NoOutput;

        Label = hasOutput
            ? output switch
            {
                CharacterOutput character when !string.IsNullOrEmpty(character.Value) => character.Value,
                SpecialKeyOutput specialKey => specialKey.Key.ToString(),
                _ => GetPhysicalLabel(KeyId)
            }
            : GetPhysicalLabel(KeyId);
        Hint = GetHint(KeyId, Mapping);
        IsUnmapped = !hasOutput;
    }

    private static string GetHint(string keyId, KeyMapping? mapping) =>
        mapping?.LogicalKey is { } logicalKey and not LogicalKey.None
            ? logicalKey.ToString()
            : keyId;

    private static string GetPhysicalLabel(string keyId) =>
        keyId.Replace("Key", string.Empty, StringComparison.Ordinal);

    private static double ToPosition(double coordinate, double unitWidth, double unitGap) =>
        coordinate * (unitWidth + unitGap);

    private static double ToLength(double length, double unitWidth, double unitGap) =>
        (length * (unitWidth + unitGap)) - unitGap;
}
