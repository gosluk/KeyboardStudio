using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class LayerMappingViewModel : ObservableObject
{
    private readonly Action<ModifierLayer, string> _updateOutput;
    private string _output;
    private string? _validationMessage;

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
        ClearCommand = new RelayCommand(() => Output = string.Empty);
    }

    public ModifierLayer Layer { get; }

    public string Label { get; }

    public IRelayCommand ClearCommand { get; }

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (SetProperty(ref _validationMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public bool HasValidationError => ValidationMessage is not null;

    public string Output
    {
        get => _output;
        set
        {
            value ??= string.Empty;
            if (SetProperty(ref _output, value))
            {
                try
                {
                    _updateOutput(Layer, value);
                    ValidationMessage = null;
                }
                catch (ArgumentException exception)
                {
                    ValidationMessage = exception.Message;
                }
            }
        }
    }
}
