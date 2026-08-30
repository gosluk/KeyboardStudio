using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class KeyViewModel : ObservableObject
{
    private string _altGrAssignment = string.Empty;
    private string _defaultAssignment = string.Empty;
    private bool _hasError;
    private bool _isAltGrActive;
    private bool _isDefaultActive;
    private bool _isSelected;
    private bool _isShiftActive;
    private bool _isShiftAltGrActive;
    private bool _isUnmapped;
    private string _shiftAssignment = string.Empty;
    private string _shiftAltGrAssignment = string.Empty;

    public KeyViewModel(
        PhysicalKey key,
        KeyMapping? mapping,
        Action<KeyViewModel> select,
        double unitWidth,
        double unitGap)
    {
        Key = key;
        Mapping = mapping;
        KeyName = GetPhysicalLabel(key.Id);
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

    public string KeyName { get; }

    public string DefaultAssignment
    {
        get => _defaultAssignment;
        private set => SetProperty(ref _defaultAssignment, value);
    }

    public string ShiftAssignment
    {
        get => _shiftAssignment;
        private set => SetProperty(ref _shiftAssignment, value);
    }

    public string AltGrAssignment
    {
        get => _altGrAssignment;
        private set => SetProperty(ref _altGrAssignment, value);
    }

    public string ShiftAltGrAssignment
    {
        get => _shiftAltGrAssignment;
        private set => SetProperty(ref _shiftAltGrAssignment, value);
    }

    public bool IsDefaultActive
    {
        get => _isDefaultActive;
        private set => SetProperty(ref _isDefaultActive, value);
    }

    public bool IsShiftActive
    {
        get => _isShiftActive;
        private set => SetProperty(ref _isShiftActive, value);
    }

    public bool IsAltGrActive
    {
        get => _isAltGrActive;
        private set => SetProperty(ref _isAltGrActive, value);
    }

    public bool IsShiftAltGrActive
    {
        get => _isShiftAltGrActive;
        private set => SetProperty(ref _isShiftAltGrActive, value);
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

    public void Refresh(ModifierLayer layer)
    {
        DefaultAssignment = GetAssignment(ModifierLayer.Default);
        ShiftAssignment = GetAssignment(ModifierLayer.Shift);
        AltGrAssignment = GetAssignment(ModifierLayer.AltGr);
        ShiftAltGrAssignment = GetAssignment(ModifierLayer.ShiftAltGr);

        IsDefaultActive = layer == ModifierLayer.Default;
        IsShiftActive = layer == ModifierLayer.Shift;
        IsAltGrActive = layer == ModifierLayer.AltGr;
        IsShiftAltGrActive = layer == ModifierLayer.ShiftAltGr;
        IsUnmapped = !HasOutput(layer);
    }

    private string GetAssignment(ModifierLayer layer)
    {
        if (Mapping?.Outputs.TryGetValue(layer, out var output) != true)
        {
            return string.Empty;
        }

        return output switch
        {
            CharacterOutput character => character.Value,
            SpecialKeyOutput specialKey => specialKey.Key.ToString(),
            _ => string.Empty
        };
    }

    private bool HasOutput(ModifierLayer layer) =>
        Mapping?.Outputs.TryGetValue(layer, out var output) == true && output is not NoOutput;

    private static string GetPhysicalLabel(string keyId) =>
        PhysicalKeyLegend.For(keyId);

    private static double ToPosition(double coordinate, double unitWidth, double unitGap) =>
        coordinate * (unitWidth + unitGap);

    private static double ToLength(double length, double unitWidth, double unitGap) =>
        (length * (unitWidth + unitGap)) - unitGap;
}
