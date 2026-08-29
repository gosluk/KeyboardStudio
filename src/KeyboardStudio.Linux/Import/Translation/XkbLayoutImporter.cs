using System.Collections.Frozen;
using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Turns a resolved symbols section into a <see cref="KeyboardProject"/>, and reports what the
/// model could not hold.
///
/// Levels map back as the exact inverse of generation: 1 to <see cref="ModifierLayer.Default"/>,
/// 2 to <see cref="ModifierLayer.Shift"/>, 3 to <see cref="ModifierLayer.AltGr"/>, 4 to
/// <see cref="ModifierLayer.ShiftAltGr"/>. Levels beyond the fourth are dropped with
/// <see cref="LayoutImportDiagnosticCodes.LayerBeyondModelDropped"/>.
///
/// The importer is deliberately ignorant of where its inputs came from: it takes a flattened
/// section and a registry hint, so the same code imports the host's database, a file the user
/// picked, and a fixture in a test.
/// </summary>
public sealed class XkbLayoutImporter
{
    /// <summary>
    /// The logical key each physical key carries by convention, independent of what a layout makes
    /// it produce. This is what stops a Dvorak import from labelling the key at <c>KeyQ</c> as the
    /// apostrophe key: the mapping records which key was pressed, and the output records what it
    /// types.
    ///
    /// It covers both templates' full key sets and agrees with the <c>us-basic</c> seed project,
    /// which is the same convention written down as data.
    /// </summary>
    private static readonly FrozenDictionary<string, LogicalKey> ConventionalLogicalKeys =
        new Dictionary<string, LogicalKey>(StringComparer.Ordinal)
        {
            ["Escape"] = LogicalKey.Escape, ["F1"] = LogicalKey.F1, ["F2"] = LogicalKey.F2,
            ["F3"] = LogicalKey.F3, ["F4"] = LogicalKey.F4, ["F5"] = LogicalKey.F5,
            ["F6"] = LogicalKey.F6, ["F7"] = LogicalKey.F7, ["F8"] = LogicalKey.F8,
            ["F9"] = LogicalKey.F9, ["F10"] = LogicalKey.F10, ["F11"] = LogicalKey.F11,
            ["F12"] = LogicalKey.F12, ["PrintScreen"] = LogicalKey.PrintScreen,
            ["ScrollLock"] = LogicalKey.ScrollLock, ["Pause"] = LogicalKey.Pause,
            ["Backquote"] = LogicalKey.Backquote, ["Digit1"] = LogicalKey.Digit1,
            ["Digit2"] = LogicalKey.Digit2, ["Digit3"] = LogicalKey.Digit3,
            ["Digit4"] = LogicalKey.Digit4, ["Digit5"] = LogicalKey.Digit5,
            ["Digit6"] = LogicalKey.Digit6, ["Digit7"] = LogicalKey.Digit7,
            ["Digit8"] = LogicalKey.Digit8, ["Digit9"] = LogicalKey.Digit9,
            ["Digit0"] = LogicalKey.Digit0, ["Minus"] = LogicalKey.Minus,
            ["Equal"] = LogicalKey.Equal, ["Backspace"] = LogicalKey.Backspace,
            ["Insert"] = LogicalKey.Insert, ["Home"] = LogicalKey.Home,
            ["PageUp"] = LogicalKey.PageUp, ["NumLock"] = LogicalKey.NumLock,
            ["NumpadDivide"] = LogicalKey.NumpadDivide, ["NumpadMultiply"] = LogicalKey.NumpadMultiply,
            ["NumpadSubtract"] = LogicalKey.NumpadSubtract, ["Tab"] = LogicalKey.Tab,
            ["KeyQ"] = LogicalKey.Q, ["KeyW"] = LogicalKey.W, ["KeyE"] = LogicalKey.E,
            ["KeyR"] = LogicalKey.R, ["KeyT"] = LogicalKey.T, ["KeyY"] = LogicalKey.Y,
            ["KeyU"] = LogicalKey.U, ["KeyI"] = LogicalKey.I, ["KeyO"] = LogicalKey.O,
            ["KeyP"] = LogicalKey.P, ["BracketLeft"] = LogicalKey.LeftBracket,
            ["BracketRight"] = LogicalKey.RightBracket, ["Enter"] = LogicalKey.Enter,
            ["Delete"] = LogicalKey.Delete, ["End"] = LogicalKey.End,
            ["PageDown"] = LogicalKey.PageDown, ["Numpad7"] = LogicalKey.Numpad7,
            ["Numpad8"] = LogicalKey.Numpad8, ["Numpad9"] = LogicalKey.Numpad9,
            ["NumpadAdd"] = LogicalKey.NumpadAdd, ["CapsLock"] = LogicalKey.CapsLock,
            ["KeyA"] = LogicalKey.A, ["KeyS"] = LogicalKey.S, ["KeyD"] = LogicalKey.D,
            ["KeyF"] = LogicalKey.F, ["KeyG"] = LogicalKey.G, ["KeyH"] = LogicalKey.H,
            ["KeyJ"] = LogicalKey.J, ["KeyK"] = LogicalKey.K, ["KeyL"] = LogicalKey.L,
            ["Semicolon"] = LogicalKey.Semicolon, ["Quote"] = LogicalKey.Quote,
            ["IntlHash"] = LogicalKey.InternationalHash, ["Numpad4"] = LogicalKey.Numpad4,
            ["Numpad5"] = LogicalKey.Numpad5, ["Numpad6"] = LogicalKey.Numpad6,
            ["ShiftLeft"] = LogicalKey.LeftShift, ["IntlBackslash"] = LogicalKey.InternationalBackslash,
            ["KeyZ"] = LogicalKey.Z, ["KeyX"] = LogicalKey.X, ["KeyC"] = LogicalKey.C,
            ["KeyV"] = LogicalKey.V, ["KeyB"] = LogicalKey.B, ["KeyN"] = LogicalKey.N,
            ["KeyM"] = LogicalKey.M, ["Comma"] = LogicalKey.Comma, ["Period"] = LogicalKey.Period,
            ["Slash"] = LogicalKey.Slash, ["ShiftRight"] = LogicalKey.RightShift,
            ["ArrowUp"] = LogicalKey.ArrowUp, ["Numpad1"] = LogicalKey.Numpad1,
            ["Numpad2"] = LogicalKey.Numpad2, ["Numpad3"] = LogicalKey.Numpad3,
            ["NumpadEnter"] = LogicalKey.NumpadEnter, ["ControlLeft"] = LogicalKey.LeftControl,
            ["MetaLeft"] = LogicalKey.LeftMeta, ["AltLeft"] = LogicalKey.LeftAlt,
            ["Space"] = LogicalKey.Space, ["AltRight"] = LogicalKey.RightAlt,
            ["MetaRight"] = LogicalKey.RightMeta, ["ContextMenu"] = LogicalKey.ContextMenu,
            ["ControlRight"] = LogicalKey.RightControl, ["ArrowLeft"] = LogicalKey.ArrowLeft,
            ["ArrowDown"] = LogicalKey.ArrowDown, ["ArrowRight"] = LogicalKey.ArrowRight,
            ["Numpad0"] = LogicalKey.Numpad0, ["NumpadDecimal"] = LogicalKey.NumpadDecimal,
            ["Backslash"] = LogicalKey.Backslash
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The four layers, indexed by XKB level. Reading the layer out of an array rather than a
    /// switch keeps the level's range check and its mapping in one place.
    /// </summary>
    private static readonly ModifierLayer[] LayersByLevel =
        [ModifierLayer.Default, ModifierLayer.Shift, ModifierLayer.AltGr, ModifierLayer.ShiftAltGr];

    private readonly IXkbKeyNameResolver _keyNameResolver;
    private readonly IXkbKeysymDecoder _keysymDecoder;
    private readonly IKeyboardTemplateProvider _templateProvider;

    public XkbLayoutImporter(
        IXkbKeyNameResolver keyNameResolver,
        IXkbKeysymDecoder keysymDecoder,
        IKeyboardTemplateProvider templateProvider)
    {
        _keyNameResolver = keyNameResolver ?? throw new ArgumentNullException(nameof(keyNameResolver));
        _keysymDecoder = keysymDecoder ?? throw new ArgumentNullException(nameof(keysymDecoder));
        _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
    }

    /// <summary>
    /// Imports one flattened section.
    /// </summary>
    /// <param name="symbols">The section with every include already merged.</param>
    /// <param name="options">The caller's choices, or <see cref="LayoutImportOptions.Default"/>.</param>
    /// <param name="registryEntry">
    /// What the registry says about the layout, used only to suggest a geometry. May be
    /// <see langword="null"/> for a layout the registry does not describe.
    /// </param>
    public LayoutImportResult Import(
        ResolvedXkbSymbols symbols,
        LayoutImportOptions options,
        XkbRegistryEntry? registryEntry)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = new List<LayoutImportDiagnostic>(symbols.Diagnostics);
        var templateId = options.TemplateId ?? XkbTemplateSelector.SelectTemplate(symbols, registryEntry);

        PhysicalKeyboard keyboard;
        try
        {
            keyboard = _templateProvider.Load(templateId);
        }
        catch (KeyboardTemplateException exception)
        {
            diagnostics.Add(new LayoutImportDiagnostic(
                ValidationSeverity.Error,
                LayoutImportDiagnosticCodes.TemplateNotAvailable,
                $"Physical keyboard template '{templateId}' could not be loaded: {exception.Message}"));

            return LayoutImportResult.Failed(new LayoutImportReport(
                LayoutImportFidelity.Partial,
                KeysImported: 0,
                KeysSkipped: symbols.Keys.Count,
                symbols.IncludeChain,
                diagnostics));
        }

        // Membership is all the projection needs, and a set turns the per-key lookup from a scan of
        // a hundred keys into a hash probe.
        var templateKeyIds = keyboard.Keys.Select(key => key.Id).ToFrozenSet(StringComparer.Ordinal);

        var layout = new KeyboardLayout();

        // Where each physical key's mapping sits, so a second name for the same key replaces the
        // first rather than adding a duplicate the editor would refuse to validate. A phonetic
        // layout is the case that needs it: am(phonetic) writes both <AD01> and <LatQ>, which
        // keycodes/evdev declares to be one key, and the host reads the later statement as the
        // one that wins.
        var mappingsByKeyId = new Dictionary<string, int>(StringComparer.Ordinal);
        var keysImported = 0;
        var keysSkipped = 0;

        foreach (var key in symbols.Keys)
        {
            var resolution = _keyNameResolver.Resolve(templateId, key.KeyName);
            if (resolution.KeyId is not { } keyId)
            {
                diagnostics.Add(resolution.Diagnostic!);
                keysSkipped++;
                continue;
            }

            if (!templateKeyIds.Contains(keyId))
            {
                diagnostics.Add(new LayoutImportDiagnostic(
                    ValidationSeverity.Info,
                    LayoutImportDiagnosticCodes.PhysicalKeyNotInTemplate,
                    $"'{key.KeyName}' is key '{keyId}', which template '{templateId}' does not have.",
                    keyId));
                keysSkipped++;
                continue;
            }

            var mapping = ProjectKey(key, keyId, diagnostics, out var everythingWasEmpty);
            if (mapping is not null)
            {
                if (mappingsByKeyId.TryGetValue(keyId, out var existing))
                {
                    layout.Mappings[existing] = mapping;
                }
                else
                {
                    mappingsByKeyId[keyId] = layout.Mappings.Count;
                    layout.Mappings.Add(mapping);
                    keysImported++;
                }
            }
            else if (!everythingWasEmpty)
            {
                // The key named outputs and none of them survived, so the key itself is a loss.
                // A key the file deliberately left blank is not, and is counted as neither.
                keysSkipped++;
            }
        }

        var project = new KeyboardProject
        {
            Metadata = new ProjectMetadata
            {
                Name = options.ProjectName ?? symbols.DisplayName ?? symbols.Section,
                Description = $"Imported from {symbols.IncludeChain[0]}."
            },
            Keyboard = keyboard,
            Layout = layout
        };

        return LayoutImportResult.Succeeded(
            project,
            templateId,
            new LayoutImportReport(
                LayoutImportReport.Classify(keysSkipped, diagnostics),
                keysImported,
                keysSkipped,
                symbols.IncludeChain,
                diagnostics));
    }

    /// <summary>
    /// Projects one resolved key onto a mapping, or <see langword="null"/> when nothing survived.
    /// </summary>
    /// <param name="everythingWasEmpty">
    /// Whether the key named no outputs the model lost — either it named none at all, or every one
    /// of them was <c>NoSymbol</c>. Distinguishes a blank key from a key whose outputs were all
    /// dropped, which the report must not count the same way.
    /// </param>
    private KeyMapping? ProjectKey(
        ResolvedXkbKey key,
        string keyId,
        List<LayoutImportDiagnostic> diagnostics,
        out bool everythingWasEmpty)
    {
        var outputs = new Dictionary<ModifierLayer, KeyOutput>();
        KeyOutput? defaultLayerOutput = null;
        everythingWasEmpty = true;

        for (var level = 0; level < key.Keysyms.Count; level++)
        {
            if (level >= LayersByLevel.Length)
            {
                everythingWasEmpty = false;
                diagnostics.Add(new LayoutImportDiagnostic(
                    ValidationSeverity.Warning,
                    LayoutImportDiagnosticCodes.LayerBeyondModelDropped,
                    $"Level {level + 1} of '{key.KeyName}' was dropped; the model holds four levels.",
                    keyId));
                continue;
            }

            var layer = LayersByLevel[level];
            var decoded = _keysymDecoder.Decode(key.Keysyms[level], keyId, layer);

            if (decoded.Diagnostic is { } diagnostic)
            {
                diagnostics.Add(diagnostic);
            }

            if (decoded.Outcome is not XkbKeysymDecodeOutcome.Empty)
            {
                everythingWasEmpty = false;
            }

            // An unrepresentable keysym leaves the layer unmapped rather than holding a NoOutput
            // that the editor would render as blank anyway; the diagnostic already records it.
            if (decoded.Output is NoOutput)
            {
                continue;
            }

            if (layer is ModifierLayer.Default)
            {
                defaultLayerOutput = decoded.Output;
            }

            outputs[layer] = decoded.Output;
        }

        if (outputs.Count == 0)
        {
            return null;
        }

        return new KeyMapping
        {
            KeyId = keyId,
            LogicalKey = DeriveLogicalKey(defaultLayerOutput, keyId),
            Outputs = outputs
        };
    }

    /// <summary>
    /// Decides which logical key a mapping records, in three steps: the default layer's own key,
    /// then the letter or digit it types, then the physical key's conventional identity.
    ///
    /// The last step is the one that matters for a rearranged layout. Dvorak's <c>KeyQ</c> types an
    /// apostrophe, which names no logical key, and without the conventional fallback every
    /// punctuation key in the layout would import as <see cref="LogicalKey.None"/>.
    /// </summary>
    private static LogicalKey DeriveLogicalKey(KeyOutput? defaultLayerOutput, string keyId)
    {
        if (defaultLayerOutput is SpecialKeyOutput { Key: not LogicalKey.None } special)
        {
            return special.Key;
        }

        if (defaultLayerOutput is CharacterOutput { Value.Length: 1 } character &&
            AsciiLogicalKey(character.Value[0]) is var typed && typed is not LogicalKey.None)
        {
            return typed;
        }

        return ConventionalLogicalKeys.GetValueOrDefault(keyId, LogicalKey.None);
    }

    /// <summary>
    /// The logical key an ASCII letter or digit names, or <see cref="LogicalKey.None"/> for
    /// anything else. Both runs are contiguous in <see cref="LogicalKey"/>, which a test pins so
    /// the arithmetic cannot drift from the enum.
    /// </summary>
    private static LogicalKey AsciiLogicalKey(char character)
    {
        if (char.IsAsciiLetter(character))
        {
            return LogicalKey.A + (char.ToUpperInvariant(character) - 'A');
        }

        return char.IsAsciiDigit(character)
            ? LogicalKey.Digit0 + (character - '0')
            : LogicalKey.None;
    }
}
