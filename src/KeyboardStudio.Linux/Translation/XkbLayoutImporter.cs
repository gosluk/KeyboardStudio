using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Imports an XKB layout into a <see cref="KeyboardProject"/>, mapping physical keys and their keysyms
/// to model outputs while tracking what was lost.
///
/// Level mapping is the inverse of generation: XKB levels 1-4 map to <see cref="ModifierLayer.Default"/>,
/// <see cref="ModifierLayer.Shift"/>, <see cref="ModifierLayer.AltGr"/>, and
/// <see cref="ModifierLayer.ShiftAltGr"/> respectively. Levels 5+ are dropped with <see cref="LayoutImportDiagnosticCodes.LayerBeyondModelDropped"/>.
///
/// Logical key derivation prefers level-1 special keys over ASCII characters over the template key's
/// conventional label, so that phonetic imports preserve their physical identity and qwerty imports do not
/// relabel every key by what it produces.
/// </summary>
public sealed class XkbLayoutImporter
{
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
    /// Imports a resolved layout into a <see cref="KeyboardProject"/>.
    /// </summary>
    /// <param name="symbols">The resolved XKB symbols after all includes and merges.</param>
    /// <param name="options">Import options including template selection and project naming.</param>
    /// <param name="registryEntry">Registry information for the layout, used for template hinting.</param>
    /// <returns>An import result with the project or diagnostics explaining why none was produced.</returns>
    public LayoutImportResult Import(
        ResolvedXkbSymbols symbols,
        LayoutImportOptions options,
        XkbRegistryEntry? registryEntry)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = new List<LayoutImportDiagnostic>(symbols.Diagnostics);

        // Select the template.
        string templateId = options.TemplateId ?? XkbTemplateSelector.SelectTemplate(symbols, registryEntry);
        PhysicalKeyboard template;
        try
        {
            template = _templateProvider.Load(templateId);
        }
        catch
        {
            diagnostics.Add(new LayoutImportDiagnostic(
                ValidationSeverity.Error,
                LayoutImportDiagnosticCodes.TemplateNotAvailable,
                $"The suggested template '{templateId}' is not available."));
            return LayoutImportResult.Failed(
                new LayoutImportReport(
                    LayoutImportFidelity.Partial,
                    KeysImported: 0,
                    KeysSkipped: symbols.Keys.Count,
                    symbols.IncludeChain,
                    diagnostics));
        }

        // Build the mapping.
        var mapping = new KeyboardLayout();
        int keysImported = 0;
        int keysSkipped = 0;

        foreach (var key in symbols.Keys)
        {
            // Resolve the key name to a physical key.
            var resolved = _keyNameResolver.Resolve(templateId, key.KeyName);
            if (!resolved.Resolved)
            {
                diagnostics.Add(resolved.Diagnostic!);
                keysSkipped++;
                continue;
            }

            var keyId = resolved.KeyId!;
            var physicalKey = template.Keys.FirstOrDefault(k => k.Id == keyId);
            if (physicalKey == null)
            {
                diagnostics.Add(new LayoutImportDiagnostic(
                    ValidationSeverity.Info,
                    LayoutImportDiagnosticCodes.PhysicalKeyNotInTemplate,
                    $"Physical key '{keyId}' resolved from '{key.KeyName}' is not in template '{templateId}'."));
                keysSkipped++;
                continue;
            }

            // Process levels.
            var layerOutputs = new Dictionary<ModifierLayer, KeyOutput>();
            var level1SpecialKey = (LogicalKey?)null;
            var level1Ascii = (char?)null;

            for (int level = 0; level < key.Keysyms.Count; level++)
            {
                var layer = LevelToLayer(level);
                if (layer == null)
                {
                    diagnostics.Add(new LayoutImportDiagnostic(
                        ValidationSeverity.Info,
                        LayoutImportDiagnosticCodes.LayerBeyondModelDropped,
                        $"Level {level + 1} of key '{key.KeyName}' was dropped because this model supports only levels 1-4.",
                        keyId,
                        ModifierLayer.Default));
                    continue;
                }

                var keysym = key.Keysyms[level];
                var decodeResult = _keysymDecoder.Decode(keysym, keyId, layer);

                if (decodeResult.Diagnostic != null)
                {
                    diagnostics.Add(decodeResult.Diagnostic);
                }

                // Capture level 1 information for logical key derivation.
                if (level == 0)
                {
                    if (decodeResult.Output is SpecialKeyOutput spo)
                    {
                        level1SpecialKey = spo.Key;
                    }
                    else if (decodeResult.Output is CharacterOutput co)
                    {
                        var text = co.Value;
                        if (text.Length == 1 && ((char.IsLetter(text[0]) && char.IsAscii(text[0])) ||
                                                   (char.IsDigit(text[0]) && char.IsAscii(text[0]))))
                        {
                            level1Ascii = text[0];
                        }
                    }
                }

                layerOutputs[layer.Value] = decodeResult.Output;
            }

            if (layerOutputs.Count == 0)
            {
                // No outputs at all for this key; skip it.
                keysSkipped++;
                continue;
            }

            // Derive the logical key.
            var conventionalKey = KeyIdToConventionalLogicalKey(keyId);
            var logicalKey = DeriveLogicalKey(
                level1SpecialKey,
                level1Ascii,
                conventionalKey);

            // Create the mapping.
            var keyMapping = new KeyMapping
            {
                KeyId = keyId,
                LogicalKey = logicalKey,
                Outputs = layerOutputs
            };

            mapping.Mappings.Add(keyMapping);
            keysImported++;
        }

        // Build the project.
        var projectName = options.ProjectName ?? (symbols.DisplayName ?? "Imported Layout");
        var project = new KeyboardProject
        {
            Metadata = new ProjectMetadata
            {
                Name = projectName,
                Description = $"Imported from {symbols.Section}"
            },
            Keyboard = new PhysicalKeyboard { Id = templateId },
            Layout = mapping
        };

        var fidelity = LayoutImportReport.Classify(keysSkipped, diagnostics);

        return LayoutImportResult.Succeeded(
            project,
            templateId,
            new LayoutImportReport(
                fidelity,
                keysImported,
                keysSkipped,
                symbols.IncludeChain,
                diagnostics));
    }

    private static ModifierLayer? LevelToLayer(int level) => level switch
    {
        0 => ModifierLayer.Default,
        1 => ModifierLayer.Shift,
        2 => ModifierLayer.AltGr,
        3 => ModifierLayer.ShiftAltGr,
        _ => null
    };

    private static LogicalKey DeriveLogicalKey(
        LogicalKey? level1SpecialKey,
        char? level1Ascii,
        LogicalKey templateKey)
    {
        // Order: level-1 SpecialKeyOutput, then ASCII letter/digit, then template key, then None.
        if (level1SpecialKey.HasValue && level1SpecialKey != LogicalKey.None)
        {
            return level1SpecialKey.Value;
        }

        if (level1Ascii.HasValue)
        {
            // Map ASCII character to logical key if possible.
            var logicalKey = CharacterToLogicalKey(level1Ascii.Value);
            if (logicalKey != LogicalKey.None)
            {
                return logicalKey;
            }
        }

        if (templateKey != LogicalKey.None)
        {
            return templateKey;
        }

        return LogicalKey.None;
    }

    private static LogicalKey CharacterToLogicalKey(char character) => char.ToUpperInvariant(character) switch
    {
        'Q' => LogicalKey.Q, 'W' => LogicalKey.W, 'E' => LogicalKey.E, 'R' => LogicalKey.R,
        'T' => LogicalKey.T, 'Y' => LogicalKey.Y, 'U' => LogicalKey.U, 'I' => LogicalKey.I,
        'O' => LogicalKey.O, 'P' => LogicalKey.P,
        'A' => LogicalKey.A, 'S' => LogicalKey.S, 'D' => LogicalKey.D, 'F' => LogicalKey.F,
        'G' => LogicalKey.G, 'H' => LogicalKey.H, 'J' => LogicalKey.J, 'K' => LogicalKey.K,
        'L' => LogicalKey.L,
        'Z' => LogicalKey.Z, 'X' => LogicalKey.X, 'C' => LogicalKey.C, 'V' => LogicalKey.V,
        'B' => LogicalKey.B, 'N' => LogicalKey.N, 'M' => LogicalKey.M,
        '1' => LogicalKey.Digit1, '2' => LogicalKey.Digit2, '3' => LogicalKey.Digit3,
        '4' => LogicalKey.Digit4, '5' => LogicalKey.Digit5, '6' => LogicalKey.Digit6,
        '7' => LogicalKey.Digit7, '8' => LogicalKey.Digit8, '9' => LogicalKey.Digit9,
        '0' => LogicalKey.Digit0,
        _ => LogicalKey.None
    };

    private static LogicalKey KeyIdToConventionalLogicalKey(string keyId) => keyId switch
    {
        // Function keys
        "F1" => LogicalKey.F1, "F2" => LogicalKey.F2, "F3" => LogicalKey.F3, "F4" => LogicalKey.F4,
        "F5" => LogicalKey.F5, "F6" => LogicalKey.F6, "F7" => LogicalKey.F7, "F8" => LogicalKey.F8,
        "F9" => LogicalKey.F9, "F10" => LogicalKey.F10, "F11" => LogicalKey.F11, "F12" => LogicalKey.F12,
        // Number row
        "Digit1" => LogicalKey.Digit1, "Digit2" => LogicalKey.Digit2, "Digit3" => LogicalKey.Digit3,
        "Digit4" => LogicalKey.Digit4, "Digit5" => LogicalKey.Digit5, "Digit6" => LogicalKey.Digit6,
        "Digit7" => LogicalKey.Digit7, "Digit8" => LogicalKey.Digit8, "Digit9" => LogicalKey.Digit9,
        "Digit0" => LogicalKey.Digit0,
        // QWERTY row
        "KeyQ" => LogicalKey.Q, "KeyW" => LogicalKey.W, "KeyE" => LogicalKey.E, "KeyR" => LogicalKey.R,
        "KeyT" => LogicalKey.T, "KeyY" => LogicalKey.Y, "KeyU" => LogicalKey.U, "KeyI" => LogicalKey.I,
        "KeyO" => LogicalKey.O, "KeyP" => LogicalKey.P,
        // ASDFGH row
        "KeyA" => LogicalKey.A, "KeyS" => LogicalKey.S, "KeyD" => LogicalKey.D, "KeyF" => LogicalKey.F,
        "KeyG" => LogicalKey.G, "KeyH" => LogicalKey.H, "KeyJ" => LogicalKey.J, "KeyK" => LogicalKey.K,
        "KeyL" => LogicalKey.L,
        // ZXCVBN row
        "KeyZ" => LogicalKey.Z, "KeyX" => LogicalKey.X, "KeyC" => LogicalKey.C, "KeyV" => LogicalKey.V,
        "KeyB" => LogicalKey.B, "KeyN" => LogicalKey.N, "KeyM" => LogicalKey.M,
        // Modifiers and special
        "ShiftLeft" => LogicalKey.LeftShift, "ShiftRight" => LogicalKey.RightShift,
        "ControlLeft" => LogicalKey.LeftControl, "ControlRight" => LogicalKey.RightControl,
        "AltLeft" => LogicalKey.LeftAlt, "AltRight" => LogicalKey.RightAlt,
        "MetaLeft" => LogicalKey.LeftMeta, "MetaRight" => LogicalKey.RightMeta,
        "Space" => LogicalKey.Space,
        "Tab" => LogicalKey.Tab, "Enter" => LogicalKey.Enter, "Escape" => LogicalKey.Escape,
        "Backspace" => LogicalKey.Backspace, "CapsLock" => LogicalKey.CapsLock,
        // Arrow keys
        "ArrowUp" => LogicalKey.ArrowUp, "ArrowDown" => LogicalKey.ArrowDown,
        "ArrowLeft" => LogicalKey.ArrowLeft, "ArrowRight" => LogicalKey.ArrowRight,
        // Navigation
        "Home" => LogicalKey.Home, "End" => LogicalKey.End, "PageUp" => LogicalKey.PageUp, "PageDown" => LogicalKey.PageDown,
        "Insert" => LogicalKey.Insert, "Delete" => LogicalKey.Delete,
        // Numpad
        "NumLock" => LogicalKey.NumLock,
        "Numpad0" => LogicalKey.Numpad0, "Numpad1" => LogicalKey.Numpad1, "Numpad2" => LogicalKey.Numpad2,
        "Numpad3" => LogicalKey.Numpad3, "Numpad4" => LogicalKey.Numpad4, "Numpad5" => LogicalKey.Numpad5,
        "Numpad6" => LogicalKey.Numpad6, "Numpad7" => LogicalKey.Numpad7, "Numpad8" => LogicalKey.Numpad8,
        "Numpad9" => LogicalKey.Numpad9,
        "NumpadAdd" => LogicalKey.NumpadAdd, "NumpadSubtract" => LogicalKey.NumpadSubtract,
        "NumpadMultiply" => LogicalKey.NumpadMultiply, "NumpadDivide" => LogicalKey.NumpadDivide,
        "NumpadDecimal" => LogicalKey.NumpadDecimal, "NumpadEnter" => LogicalKey.NumpadEnter,
        // Special
        "PrintScreen" => LogicalKey.PrintScreen, "ScrollLock" => LogicalKey.ScrollLock,
        "Pause" => LogicalKey.Pause, "ContextMenu" => LogicalKey.ContextMenu,
        _ => LogicalKey.None
    };
}
