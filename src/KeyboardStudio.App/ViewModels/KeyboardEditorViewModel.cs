using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class KeyboardEditorViewModel : ObservableObject
{
    private readonly KeyboardEditor _editor;
    private ModifierLayer _activeLayer;
    private KeyViewModel? _selectedKey;

    public KeyboardEditorViewModel(KeyboardEditor editor, KeyboardTemplateDescriptor template)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(template);

        _editor = editor;
        Layers = Enum.GetValues<ModifierLayer>();
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

        RefreshLabels();
        SelectedKey = Keys.FirstOrDefault();
    }

    public ObservableCollection<KeyViewModel> Keys { get; }
    public IReadOnlyList<ModifierLayer> Layers { get; }
    public double KeyboardWidth { get; }
    public double KeyboardHeight { get; }

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
