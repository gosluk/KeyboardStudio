using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

/// <summary>
/// One layer of the selected key, as an editable output of either kind.
/// </summary>
/// <remarks>
/// This row used to hold a bare string, which made it the one reader in the application that
/// narrowed <see cref="KeyOutput"/> to <see cref="CharacterOutput"/>. A functional output — what an
/// import produces in quantity, and what the keycap and its tooltip have always drawn — arrived here
/// as an empty box: the panel showed nothing, Clear could not remove what it was not showing, and
/// the first character typed replaced the assignment without a word. Carrying the kind alongside the
/// value is what closes that, and it costs one picker.
/// </remarks>
public sealed class LayerMappingViewModel : ObservableObject
{
    private readonly Action<ModifierLayer, KeyOutput?> _updateOutput;
    private readonly Func<KeyOutput?, string?> _describeCapability;
    private string _output;
    private KeyOutputKind _kind;
    private LogicalKeyOptionViewModel? _specialKey;
    private string? _validationMessage;
    private string? _capabilityWarning;

    public LayerMappingViewModel(
        ModifierLayerOptionViewModel layer,
        KeyOutput? output,
        IReadOnlyList<KeyOutputKindOptionViewModel> kinds,
        IReadOnlyList<LogicalKeyOptionViewModel> specialKeys,
        Action<ModifierLayer, KeyOutput?> updateOutput,
        Func<KeyOutput?, string?> describeCapability)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(kinds);
        ArgumentNullException.ThrowIfNull(specialKeys);
        ArgumentNullException.ThrowIfNull(updateOutput);
        ArgumentNullException.ThrowIfNull(describeCapability);

        Layer = layer.Value;
        Label = layer.Label;
        Kinds = kinds;
        SpecialKeys = specialKeys;
        _updateOutput = updateOutput;
        _describeCapability = describeCapability;

        // The row opens showing what the mapping actually holds, whichever kind that is.
        (_kind, _output, _specialKey) = output switch
        {
            CharacterOutput character => (KeyOutputKind.Character, character.Value, null),
            SpecialKeyOutput special => (
                KeyOutputKind.SpecialKey,
                string.Empty,
                specialKeys.FirstOrDefault(option => option.Key == special.Key)),
            _ => (KeyOutputKind.None, string.Empty, (LogicalKeyOptionViewModel?)null)
        };

        _capabilityWarning = describeCapability(output);
        ClearCommand = new RelayCommand(Clear);
    }

    public ModifierLayer Layer { get; }

    public string Label { get; }

    public IReadOnlyList<KeyOutputKindOptionViewModel> Kinds { get; }

    public IReadOnlyList<LogicalKeyOptionViewModel> SpecialKeys { get; }

    public IRelayCommand ClearCommand { get; }

    /// <summary>Which kind of output this layer produces. Changing it commits immediately.</summary>
    public KeyOutputKind Kind
    {
        get => _kind;
        set
        {
            if (SetProperty(ref _kind, value))
            {
                OnPropertyChanged(nameof(KindOption));
                OnPropertyChanged(nameof(IsCharacter));
                OnPropertyChanged(nameof(IsSpecialKey));
                Commit();
            }
        }
    }

    /// <summary>The kind picker's selection, which is <see cref="Kind"/> wearing a label.</summary>
    public KeyOutputKindOptionViewModel? KindOption
    {
        get => Kinds.FirstOrDefault(option => option.Kind == Kind);
        set
        {
            if (value is not null)
            {
                Kind = value.Kind;
            }
        }
    }

    public bool IsCharacter => Kind == KeyOutputKind.Character;

    public bool IsSpecialKey => Kind == KeyOutputKind.SpecialKey;

    /// <summary>
    /// The character this layer types. Typing into an empty row selects the character kind, so the
    /// common case still costs one keystroke and no visit to the picker.
    /// </summary>
    public string Output
    {
        get => _output;
        set
        {
            value ??= string.Empty;
            if (!SetProperty(ref _output, value))
            {
                return;
            }

            if (value.Length > 0 && Kind != KeyOutputKind.Character)
            {
                Kind = KeyOutputKind.Character;
                return;
            }

            Commit();
        }
    }

    /// <summary>The functional key this layer produces.</summary>
    public LogicalKeyOptionViewModel? SpecialKey
    {
        get => _specialKey;
        set
        {
            if (!SetProperty(ref _specialKey, value))
            {
                return;
            }

            if (value is not null && Kind != KeyOutputKind.SpecialKey)
            {
                Kind = KeyOutputKind.SpecialKey;
                return;
            }

            Commit();
        }
    }

    /// <summary>Why the value in hand is not a valid output at all.</summary>
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

    /// <summary>
    /// Why a build target would refuse an otherwise valid output. Advisory: the assignment is kept
    /// either way, and the build's own validation remains the authority.
    /// </summary>
    public string? CapabilityWarning
    {
        get => _capabilityWarning;
        private set
        {
            if (SetProperty(ref _capabilityWarning, value))
            {
                OnPropertyChanged(nameof(HasCapabilityWarning));
            }
        }
    }

    public bool HasCapabilityWarning => CapabilityWarning is not null;

    private void Clear()
    {
        // Set through the fields so a row that is already visibly empty still reaches Commit: a
        // functional output used to survive Clear precisely because the box it was not shown in had
        // not changed.
        SetProperty(ref _output, string.Empty, nameof(Output));
        SetProperty(ref _specialKey, null, nameof(SpecialKey));
        Kind = KeyOutputKind.None;
        Commit();
    }

    private void Commit()
    {
        KeyOutput? output;
        try
        {
            output = Kind switch
            {
                KeyOutputKind.Character when _output.Length > 0 => new CharacterOutput(_output),
                KeyOutputKind.SpecialKey when _specialKey is not null => new SpecialKeyOutput(_specialKey.Key),
                _ => null
            };
        }
        catch (ArgumentException exception)
        {
            ValidationMessage = exception.Message;
            CapabilityWarning = null;
            return;
        }

        ValidationMessage = null;
        CapabilityWarning = _describeCapability(output);
        _updateOutput(Layer, output);
    }
}
