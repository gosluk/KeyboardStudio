using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class KeyViewModel : ObservableObject
{
    private string _label;

    public KeyViewModel(PhysicalKey key, KeyMapping? mapping, Action<KeyViewModel> select)
    {
        Key = key;
        Mapping = mapping;
        _label = key.Id.Replace("Key", string.Empty, StringComparison.Ordinal);
        SelectCommand = new RelayCommand(() => select(this));
    }

    public PhysicalKey Key { get; }
    public string KeyId => Key.Id;
    public string ScanCode => $"0x{Key.ScanCode:X2}";
    public KeyMapping? Mapping { get; set; }
    public IRelayCommand SelectCommand { get; }

    public string Label
    {
        get => _label;
        private set => SetProperty(ref _label, value);
    }

    public void Refresh(ModifierLayer layer)
    {
        Label = Mapping?.Outputs.TryGetValue(layer, out var output) == true
            ? output switch
            {
                CharacterOutput character when !string.IsNullOrEmpty(character.Value) => character.Value,
                SpecialKeyOutput specialKey => specialKey.Key.ToString(),
                _ => KeyId.Replace("Key", string.Empty, StringComparison.Ordinal)
            }
            : KeyId.Replace("Key", string.Empty, StringComparison.Ordinal);
    }
}
