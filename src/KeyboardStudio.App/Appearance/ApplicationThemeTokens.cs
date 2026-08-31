namespace KeyboardStudio.App;

/// <summary>
/// The semantic resource keys every application theme must define.
/// </summary>
/// <remarks>
/// A theme dictionary that omits a key does not fail the build. The application simply resolves
/// that key against the inherited Fluent variant and draws one state of one surface in a colour
/// that belongs to a different palette, which is the kind of defect nobody finds by looking at a
/// diff. This list is the contract a test holds every dictionary to instead.
/// </remarks>
public static class ApplicationThemeTokens
{
    /// <summary>Every resource key each theme dictionary is required to define, and no others.</summary>
    public static IReadOnlyList<string> Required { get; } =
    [
        // Surfaces
        "AppSurfaceBrush",
        "WorkspaceSurfaceBrush",
        "PanelSurfaceBrush",
        "ElevatedSurfaceBrush",
        // Borders
        "SubtleBorderBrush",
        "AppBorderBrush",
        "StrongBorderBrush",
        "SelectedBorderBrush",
        "FocusBorderBrush",
        // Foreground
        "PrimaryForegroundBrush",
        "SecondaryForegroundBrush",
        "DisabledForegroundBrush",
        "InverseForegroundBrush",
        "LinkForegroundBrush",
        "HeadingForegroundBrush",
        "MutedForegroundBrush",
        // Accent
        "AccentBrush",
        "AccentHoverBrush",
        "AccentPressedBrush",
        "AccentForegroundBrush",
        // Status
        "SuccessSurfaceBrush",
        "SuccessForegroundBrush",
        "SuccessBorderBrush",
        "WarningSurfaceBrush",
        "WarningForegroundBrush",
        "WarningBorderBrush",
        "DangerSurfaceBrush",
        "DangerForegroundBrush",
        "DangerBorderBrush",
        "InfoSurfaceBrush",
        "InfoForegroundBrush",
        "InfoBorderBrush",
        // Cards
        "CardBackgroundBrush",
        "CardBorderBrush",
        // Menus
        "MenuSurfaceBrush",
        "MenuBorderBrush",
        "MenuForegroundBrush",
        "MenuHoverSurfaceBrush",
        "MenuSeparatorBrush",
        // Tooltips
        "TooltipSurfaceBrush",
        "TooltipBorderBrush",
        "TooltipForegroundBrush",
        // Badges
        "BadgeBackgroundBrush",
        "BadgeForegroundBrush",
        "BadgeBorderBrush",
        // Inputs
        "InputSurfaceBrush",
        "InputBorderBrush",
        "InputForegroundBrush",
        "InputPlaceholderBrush",
        "InputFocusBorderBrush",
        "InputHoverSurfaceBrush",
        "InputDisabledSurfaceBrush",
        // Buttons
        "ButtonSurfaceBrush",
        "ButtonBorderBrush",
        "ButtonForegroundBrush",
        "ButtonHoverSurfaceBrush",
        "ButtonPressedSurfaceBrush",
        "ButtonDisabledSurfaceBrush",
        "ButtonDisabledForegroundBrush",
        "ButtonDisabledBorderBrush",
        "QuietButtonHoverSurfaceBrush",
        "DestructiveButtonSurfaceBrush",
        "DestructiveButtonBorderBrush",
        "DestructiveButtonForegroundBrush",
        "DestructiveButtonHoverSurfaceBrush",
        "DestructiveButtonPressedSurfaceBrush",
        // Selection
        "SelectionSurfaceBrush",
        "SelectionForegroundBrush",
        "SelectionBorderBrush",
        // Diagnostics
        "DiagnosticSurfaceBrush",
        "DiagnosticHoverSurfaceBrush",
        "DiagnosticBorderBrush",
        // Build problems
        "BuildProblemSurfaceBrush",
        "BuildProblemBorderBrush",
        "BuildProblemForegroundBrush",
        // Document state
        "DirtyBadgeSurfaceBrush",
        "DirtyBadgeForegroundBrush",
        "DirtyBadgeBorderBrush",
        "ImportStatusForegroundBrush",
        // Keyboard
        "KeyboardBezelBrush",
        "KeyboardBezelBorderBrush",
        "KeyFaceBrush",
        "KeyFaceHoverBrush",
        "KeyFacePressedBrush",
        "KeyBorderBrush",
        "KeyLegendBrush",
        "KeyHintBrush",
        "KeyActiveLayerBrush",
        "KeyActiveLegendBrush",
        "KeySelectedFaceBrush",
        "KeySelectedBorderBrush",
        "KeyErrorFaceBrush",
        "KeyErrorBorderBrush",
        // Elevation
        "CardBoxShadow",
        "KeyBoxShadow",
        "ElevatedBoxShadow",
    ];
}
