using CommunityToolkit.Mvvm.ComponentModel;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class LayerMappingViewModel : ObservableObject
{
    private readonly Action<ModifierLayer, string> _updateOutput;
    private string _output;

    public LayerMappingViewModel(
        ModifierLayerOptionViewModel layer,
        string output,
        Action<ModifierLayer, string> updateOutput)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(updateOutput);

        Layer = layer.Value;
        Label = layer.Label;
        _output = output;
        _updateOutput = updateOutput;
    }

    public ModifierLayer Layer { get; }

    public string Label { get; }

    public string Output
    {
        get => _output;
        set
        {
            value ??= string.Empty;
            if (SetProperty(ref _output, value))
            {
                _updateOutput(Layer, value);
            }
        }
    }
}
