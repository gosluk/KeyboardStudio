using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class KeyboardEditorViewModel : ObservableObject
{
    private static readonly IReadOnlyList<ModifierLayerOptionViewModel> ModifierLayers =
    [
        new(ModifierLayer.Default, "Default"),
        new(ModifierLayer.Shift, "Shift"),
        new(ModifierLayer.AltGr, "AltGr"),
        new(ModifierLayer.ShiftAltGr, "Shift + AltGr")
    ];

    private static readonly IReadOnlyList<LogicalKey> EditableLogicalKeys =
        Enum.GetValues<LogicalKey>();

    private readonly KeyboardEditor _editor;
    private ModifierLayer _activeLayer;
    private IReadOnlyList<LayerMappingViewModel> _layerMappings = [];
    private KeyViewModel? _selectedKey;

    public KeyboardEditorViewModel(KeyboardEditor editor, KeyboardTemplateDescriptor template)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(template);

        _editor = editor;
        Layers = ModifierLayers;
        Keys = new ObservableCollection<KeyViewModel>(
            editor.Project.Keyboard.Keys.Select(key =>
                new KeyViewModel(
                    key,
                    editor.Project.Layout.Find(key.Id),
                    SelectKey,
                    template.UnitWidth,
                    template.UnitGap)));

        KeyboardWidth = Keys.Select(key => key.Left + key.Width).DefaultIfEmpty().Max();
        KeyboardHeight = Keys.Select(key => key.Top + key.Height).DefaultIfEmpty().Max();
        ClearAllOutputsCommand = new RelayCommand(ClearAllOutputs, () => SelectedKey is not null);
        UnmapLogicalKeyCommand = new RelayCommand(UnmapLogicalKey, () => SelectedKey is not null);

        RefreshLabels();
        SelectedKey = Keys.FirstOrDefault();
    }

    public ObservableCollection<KeyViewModel> Keys { get; }
    public IReadOnlyList<ModifierLayerOptionViewModel> Layers { get; }
    public IReadOnlyList<LogicalKey> LogicalKeys => EditableLogicalKeys;
    public double KeyboardWidth { get; }
    public double KeyboardHeight { get; }
    public IRelayCommand ClearAllOutputsCommand { get; }
    public IRelayCommand UnmapLogicalKeyCommand { get; }

    public IReadOnlyList<LayerMappingViewModel> LayerMappings
    {
        get => _layerMappings;
        private set => SetProperty(ref _layerMappings, value);
    }

    public LogicalKey SelectedLogicalKey
    {
        get => SelectedKey?.Mapping?.LogicalKey ?? LogicalKey.None;
        set
        {
            if (SelectedKey is null || value == SelectedLogicalKey)
            {
                return;
            }

            _editor.MapLogicalKey(SelectedKey.KeyId, value);
            SelectedKey.Mapping = _editor.Project.Layout.Find(SelectedKey.KeyId);
            SelectedKey.Refresh(ActiveLayer);
            OnPropertyChanged();
        }
    }

    public ModifierLayer ActiveLayer
    {
        get => _activeLayer;
        set
        {
            if (SetProperty(ref _activeLayer, value))
            {
                RefreshLabels();
                OnPropertyChanged(nameof(ActiveLayerOption));
                OnPropertyChanged(nameof(SelectedOutput));
            }
        }
    }

    public ModifierLayerOptionViewModel ActiveLayerOption
    {
        get => Layers.Single(option => option.Value == ActiveLayer);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ActiveLayer = value.Value;
        }
    }

    public KeyViewModel? SelectedKey
    {
        get => _selectedKey;
        private set
        {
            var previousKey = _selectedKey;
            if (SetProperty(ref _selectedKey, value))
            {
                if (previousKey is not null)
                {
                    previousKey.IsSelected = false;
                }

                if (value is not null)
                {
                    value.IsSelected = true;
                }

                RefreshMappingPanel();
                OnPropertyChanged(nameof(SelectedLogicalKey));
                OnPropertyChanged(nameof(SelectedOutput));
                ClearAllOutputsCommand.NotifyCanExecuteChanged();
                UnmapLogicalKeyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool SelectKey(string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        var key = Keys.FirstOrDefault(candidate =>
            string.Equals(candidate.KeyId, keyId, StringComparison.Ordinal));
        if (key is null)
        {
            return false;
        }

        SelectedKey = key;
        return true;
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

    private void RefreshMappingPanel()
    {
        LayerMappings = Layers
            .Select(layer => new LayerMappingViewModel(
                layer,
                GetCharacterOutput(layer.Value),
                UpdateOutput))
            .ToArray();
    }

    private string GetCharacterOutput(ModifierLayer layer) =>
        SelectedKey?.Mapping?.Outputs.TryGetValue(layer, out var output) == true &&
        output is CharacterOutput characterOutput
            ? characterOutput.Value
            : string.Empty;

    private void UpdateOutput(ModifierLayer layer, string output)
    {
        if (SelectedKey is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(output))
        {
            _editor.ClearMapping(SelectedKey.KeyId, layer);
        }
        else
        {
            _editor.MapCharacter(SelectedKey.KeyId, layer, output);
        }

        SelectedKey.Mapping = _editor.Project.Layout.Find(SelectedKey.KeyId);
        SelectedKey.Refresh(ActiveLayer);
        OnPropertyChanged(nameof(SelectedLogicalKey));
        OnPropertyChanged(nameof(SelectedOutput));
    }

    private void ClearAllOutputs()
    {
        if (SelectedKey is null)
        {
            return;
        }

        _editor.ClearAllOutputs(SelectedKey.KeyId);
        SelectedKey.Mapping = _editor.Project.Layout.Find(SelectedKey.KeyId);
        SelectedKey.Refresh(ActiveLayer);
        RefreshMappingPanel();
        OnPropertyChanged(nameof(SelectedOutput));
    }

    private void UnmapLogicalKey()
    {
        SelectedLogicalKey = LogicalKey.None;
    }

    private void RefreshLabels()
    {
        foreach (var key in Keys)
        {
            key.Refresh(ActiveLayer);
        }
    }
}
