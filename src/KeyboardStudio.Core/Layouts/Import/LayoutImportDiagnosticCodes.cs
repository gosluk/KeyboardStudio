namespace KeyboardStudio.Core;

/// <summary>
/// Stable diagnostic codes for layout import, in the <c>KSI</c> range.
///
/// The codes live in Core, alongside the import contract they belong to, so that every source
/// reports the same loss the same way and a second source cannot quietly reuse a number for a
/// different meaning. Their wording is source-neutral for the same reason: the concepts here —
/// a layout the model cannot fully represent — outlive any one platform's file format.
///
/// The range is grouped: <c>KSI0xx</c> covers cataloguing, <c>KSI02x</c> covers reading a source
/// definition, and <c>KSI03x</c> covers translating one into the domain model. Per
/// <c>docs/DIAGNOSTICS.md</c>, meanings are never reassigned and retired codes stay reserved.
/// </summary>
public static class LayoutImportDiagnosticCodes
{
    /// <summary>
    /// Info. The layout exists but the source has no descriptive metadata for it, so it is listed
    /// under its bare identifier.
    /// </summary>
    public const string LayoutMetadataUnavailable = "KSI010";

    /// <summary>
    /// Warning. The definition carried more than one alternative group of outputs per key. Only the
    /// primary group was imported; the model has no place for the rest.
    /// </summary>
    public const string AlternateGroupsIgnored = "KSI020";

    /// <summary>
    /// Warning. The definition used a construct the model cannot express — a key action, a
    /// redirection, an overlay — which was read and then ignored.
    /// </summary>
    public const string UnsupportedConstructIgnored = "KSI021";

    /// <summary>
    /// Info. A statement the reader does not recognize was skipped. Import continues: the goal is a
    /// usable starting point, not a conformant compiler.
    /// </summary>
    public const string UnrecognizedStatementSkipped = "KSI022";

    /// <summary>
    /// Info. A composition rule was approximated by the nearest one the resolver implements, which
    /// may change the result for keys that two definitions both describe.
    /// </summary>
    public const string MergeModeApproximated = "KSI023";

    /// <summary>
    /// Error. A definition composed from others nested deeper than the resolver's cap, which guards
    /// against pathological or circular data. Nothing was imported.
    /// </summary>
    public const string CompositionDepthExceeded = "KSI024";

    /// <summary>
    /// Warning. A definition a layout composes from could not be contributed: either no source
    /// holds it, or it repeats one already being read and would compose forever.
    ///
    /// The rest of the layout is still imported. A missing piece leaves gaps the user can see and
    /// fill, whereas refusing the whole import leaves them nothing to work from.
    /// </summary>
    public const string CompositionTargetUnavailable = "KSI025";

    /// <summary>
    /// Warning. The definition assigned an output to a modifier level beyond the four the model
    /// has. The output was dropped; the key's other levels were kept.
    /// </summary>
    public const string LayerBeyondModelDropped = "KSI030";

    /// <summary>
    /// Warning. The output was a dead key, which the model does not represent. The layer was left
    /// unmapped rather than given a misleading character.
    /// </summary>
    public const string DeadKeyDropped = "KSI031";

    /// <summary>
    /// Warning. The output has no equivalent in the model — neither a character nor a known logical
    /// key — so the layer was left unmapped.
    /// </summary>
    public const string OutputNotRepresentable = "KSI032";

    /// <summary>
    /// Info. The definition described a key the chosen physical keyboard template does not have, so
    /// the whole key was skipped. Expected for media and vendor keys.
    /// </summary>
    public const string PhysicalKeyNotInTemplate = "KSI033";

    /// <summary>
    /// Error. The suggested physical keyboard template is not available on this host. No import
    /// is possible.
    /// </summary>
    public const string TemplateNotAvailable = "KSI034";
}
