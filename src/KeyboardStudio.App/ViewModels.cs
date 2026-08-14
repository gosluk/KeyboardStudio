using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Build;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class MainWindowViewModel
{
    public MainWindowViewModel()
    {
        Project = DemoProjectFactory.Create();
        Editor = new KeyboardEditorViewModel(new KeyboardEditor(Project));
        Build = new BuildViewModel(new WindowsBuildEnvironment());
    }

    public KeyboardProject Project { get; }
    public KeyboardEditorViewModel Editor { get; }
    public BuildViewModel Build { get; }
}

public sealed class KeyboardEditorViewModel : ObservableObject
{
    private readonly KeyboardEditor _editor;
    private ModifierLayer _activeLayer;
    private KeyViewModel? _selectedKey;

    public KeyboardEditorViewModel(KeyboardEditor editor)
    {
        _editor = editor;
        Layers = Enum.GetValues<ModifierLayer>();
        Keys = new ObservableCollection<KeyViewModel>(
            editor.Project.Keyboard.Keys.Select(key =>
                new KeyViewModel(key, editor.Project.Layout.Find(key.Id), SelectKey)));

        RefreshLabels();
        SelectedKey = Keys.FirstOrDefault();
    }

    public ObservableCollection<KeyViewModel> Keys { get; }
    public IReadOnlyList<ModifierLayer> Layers { get; }

    public ModifierLayer ActiveLayer
    {
        get => _activeLayer;
        set
        {
            if (SetProperty(ref _activeLayer, value))
            {
                RefreshLabels();
                OnPropertyChanged(nameof(SelectedOutput));
            }
        }
    }

    public KeyViewModel? SelectedKey
    {
        get => _selectedKey;
        private set
        {
            if (SetProperty(ref _selectedKey, value))
            {
                OnPropertyChanged(nameof(SelectedOutput));
            }
        }
    }

    public string SelectedOutput
    {
        get
        {
            if (SelectedKey?.Mapping?.Outputs.TryGetValue(ActiveLayer, out var output) == true &&
                output is CharacterOutput characterOutput)
            {
                return characterOutput.Value;
            }

            return string.Empty;
        }
        set
        {
            if (SelectedKey is null)
            {
                return;
            }

            if (string.IsNullOrEmpty(value))
            {
                _editor.ClearMapping(SelectedKey.KeyId, ActiveLayer);
            }
            else
            {
                _editor.MapCharacter(SelectedKey.KeyId, ActiveLayer, value);
            }

            SelectedKey.Mapping = _editor.Project.Layout.Find(SelectedKey.KeyId);
            SelectedKey.Refresh(ActiveLayer);
            OnPropertyChanged();
        }
    }

    private void SelectKey(KeyViewModel key)
    {
        SelectedKey = key;
    }

    private void RefreshLabels()
    {
        foreach (var key in Keys)
        {
            key.Refresh(ActiveLayer);
        }
    }
}

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

public sealed class BuildViewModel
{
    public BuildViewModel(IBuildEnvironment environment)
    {
        var status = environment.GetStatus(BuildTarget.WindowsX64);
        Status = status.Message;
    }

    public string Status { get; }
}
