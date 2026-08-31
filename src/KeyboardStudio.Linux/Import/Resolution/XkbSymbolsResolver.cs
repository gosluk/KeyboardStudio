using System.Text;
using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Walks a section's includes and merges every definition into one flat set of keys.
///
/// Real layouts are composed rather than written out: <c>pl(basic)</c> is <c>latin</c> plus its own
/// changes, and <c>latin</c> is itself built from <c>us</c>. Flattening that chain is what turns a
/// handful of overrides into the layout a user actually types on.
/// </summary>
public sealed class XkbSymbolsResolver : IXkbSymbolsResolver
{
    /// <summary>
    /// How deep composition may nest. The deepest chain in a stock xkeyboard-config is well under
    /// this, so the cap exists to stop pathological or generated data rather than to constrain
    /// anything real.
    /// </summary>
    public const int MaximumDepth = 16;

    private readonly IXkbFileSystem _fileSystem;
    private readonly IXkbIncludeResolver _includeResolver;
    private readonly XkbSymbolsParser _parser = new();

    /// <summary>
    /// Parsed files, keyed by resolved path. A layout that includes three sections of one file
    /// should read and parse that file once, not three times.
    /// </summary>
    private readonly Dictionary<string, XkbSymbolsFile?> _fileCache = new(StringComparer.Ordinal);

    public XkbSymbolsResolver(IXkbFileSystem fileSystem, IXkbIncludeResolver includeResolver)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(includeResolver);

        _fileSystem = fileSystem;
        _includeResolver = includeResolver;
    }

    public ResolvedXkbSymbols? Resolve(string file, string? section) =>
        Resolve(file, section, composeCommonBase: false);

    public ResolvedXkbSymbols? ResolveLayout(string file, string? section) =>
        Resolve(file, section, composeCommonBase: true);

    private ResolvedXkbSymbols? Resolve(string file, string? section, bool composeCommonBase)
    {
        ArgumentNullException.ThrowIfNull(file);

        var path = _includeResolver.ResolveFilePath(file);
        if (path is null)
        {
            return null;
        }

        var parsed = ReadFile(path);
        var target = section is null ? parsed?.DefaultSection : parsed?.FindSection(section);
        if (parsed is null || target is null)
        {
            return null;
        }

        var state = new ResolutionState();

        if (composeCommonBase)
        {
            MergeCommonBase(state);
        }

        // The visited set holds path-and-section pairs rather than paths. A file including another
        // of its own sections is normal — pl(lefty) includes pl(basic) — so a file-granular set
        // would break those layouts while still missing cycles that run through two files.
        Merge(path, target, XkbMergeMode.Override, depth: 0, state);

        return new ResolvedXkbSymbols(
            path,
            target.Name,
            state.DisplayName,
            state.OrderedKeys(),
            state.Chain,
            state.Diagnostics);
    }

    /// <summary>
    /// Merges <see cref="XkbCommonBase"/> before the layout, so that the layout's own definitions
    /// override it exactly as the <c>pc+%l</c> composition in the rules does.
    /// </summary>
    /// <remarks>
    /// A root that does not ship the base contributes nothing and is not a finding: the base is an
    /// inference this resolver makes on the layout's behalf, not something the layout asked for. A
    /// test fixture holding two symbols files is the ordinary case of that.
    /// </remarks>
    private void MergeCommonBase(ResolutionState state)
    {
        var path = _includeResolver.ResolveFilePath(XkbCommonBase.FileName);
        var target = path is null ? null : ReadFile(path)?.DefaultSection;
        if (path is null || target is null)
        {
            return;
        }

        state.MergingCommonBase = true;
        Merge(path, target, XkbMergeMode.Override, depth: 0, state);
        state.MergingCommonBase = false;
    }

    /// <summary>
    /// Reads and parses a file, remembering the outcome — including the failure, so an unreadable
    /// file is not retried once per include that names it.
    /// </summary>
    private XkbSymbolsFile? ReadFile(string path)
    {
        if (_fileCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        XkbSymbolsFile? parsed;
        try
        {
            using var stream = _fileSystem.OpenRead(path);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            parsed = _parser.Parse(path, reader.ReadToEnd());
        }
        catch (IOException)
        {
            parsed = null;
        }
        catch (UnauthorizedAccessException)
        {
            parsed = null;
        }

        _fileCache[path] = parsed;
        return parsed;
    }

    /// <summary>
    /// Merges one section into the accumulated state, recursing through its includes first so that
    /// the section's own statements are applied on top of what it composes.
    /// </summary>
    private void Merge(string path, XkbSymbolsSection section, XkbMergeMode merge, int depth, ResolutionState state)
    {
        var origin = FormatOrigin(path, section.Name);

        if (depth >= MaximumDepth)
        {
            state.Diagnostics.Add(new LayoutImportDiagnostic(
                ValidationSeverity.Error,
                LayoutImportDiagnosticCodes.CompositionDepthExceeded,
                $"'{origin}' is nested more than {MaximumDepth} levels deep, so it was not read."));
            return;
        }

        var key = (path, section.Name);
        if (!state.Visiting.Add(key))
        {
            // A genuine cycle. Stopping here and reporting it keeps the rest of the layout usable,
            // which is more useful than failing the import over one bad edge in the graph.
            state.Diagnostics.Add(new LayoutImportDiagnostic(
                ValidationSeverity.Warning,
                LayoutImportDiagnosticCodes.CompositionTargetUnavailable,
                $"'{origin}' includes itself, so the repeated inclusion was skipped."));
            return;
        }

        state.Chain.Add(origin);

        // The parser's own findings belong to whoever composes the definition, so they travel with
        // it — but only the ones from the section actually being merged. A file is read whole and
        // most of it is not used: `keypad` holds four overlay sections no layout composes, and
        // reporting their losses against a layout that merged only `keypad(x11)` would describe
        // something that never happened.
        var parsed = _fileCache[path];
        if (parsed is not null && state.ReportedFiles.Add(path))
        {
            state.Diagnostics.AddRange(parsed.Diagnostics);
        }

        if (state.ReportedSections.Add(key))
        {
            state.Diagnostics.AddRange(section.Diagnostics);
        }

        foreach (var statement in section.Statements)
        {
            switch (statement)
            {
                case XkbIncludeStatement include:
                    MergeInclude(include, depth, state);
                    break;

                case XkbKeyStatement keyStatement:
                    ApplyKey(keyStatement, EffectiveMerge(keyStatement.Merge, merge, state), origin, state);
                    break;

                case XkbNameStatement when state.MergingCommonBase:
                    // The base names no group of its own, and a base that did would be naming
                    // every layout composed onto it.
                    break;

                case XkbNameStatement name when name.Group == 1:
                    state.DisplayName = name.Value;
                    break;

                default:
                    break;
            }
        }

        state.Visiting.Remove(key);
    }

    /// <summary>Resolves one include statement and merges each file it names.</summary>
    private void MergeInclude(XkbIncludeStatement include, int depth, ResolutionState state)
    {
        var specs = _includeResolver.Parse(include.Specification, include.Merge);
        if (specs.Count == 0)
        {
            state.Diagnostics.Add(new LayoutImportDiagnostic(
                ValidationSeverity.Warning,
                LayoutImportDiagnosticCodes.CompositionTargetUnavailable,
                $"The include '{include.Specification}' names nothing that could be resolved."));
            return;
        }

        foreach (var spec in specs)
        {
            if (spec.Group != 1)
            {
                // Loading it into group 1 anyway would overwrite the layout the user asked for with
                // a secondary one, which is worse than not having it.
                state.Diagnostics.Add(new LayoutImportDiagnostic(
                    ValidationSeverity.Warning,
                    LayoutImportDiagnosticCodes.AlternateGroupsIgnored,
                    $"'{spec.File}' was included into group {spec.Group}, which the model does not hold, so it was skipped."));
                continue;
            }

            var path = _includeResolver.ResolveFilePath(spec.File);
            var parsed = path is null ? null : ReadFile(path);
            var target = spec.Section is null ? parsed?.DefaultSection : parsed?.FindSection(spec.Section);

            if (path is null || parsed is null || target is null)
            {
                state.Diagnostics.Add(new LayoutImportDiagnostic(
                    ValidationSeverity.Warning,
                    LayoutImportDiagnosticCodes.CompositionTargetUnavailable,
                    $"The included definition '{FormatSpec(spec)}' was not found in any XKB directory."));
                continue;
            }

            Merge(path, target, EffectiveMerge(spec.Merge, XkbMergeMode.Override, state), depth + 1, state);
        }
    }

    /// <summary>Applies one key definition according to the rule in force.</summary>
    private static void ApplyKey(XkbKeyStatement statement, XkbMergeMode merge, string origin, ResolutionState state)
    {
        var existing = state.Keys.TryGetValue(statement.KeyName, out var current) ? current : null;

        if (existing is not null && merge == XkbMergeMode.Augment)
        {
            // Augment adds what is missing and changes nothing that is already there.
            return;
        }

        // A statement that carries no keysyms set only properties the model does not hold, such as a
        // key type. Under override that leaves the existing outputs alone; under replace the whole
        // definition goes, which is the one place the two modes visibly differ.
        if (existing is not null && statement.Keysyms.Count == 0 && merge != XkbMergeMode.Replace)
        {
            return;
        }

        state.Keys[statement.KeyName] = new ResolvedXkbKey(
            statement.KeyName,
            statement.Keysyms,
            origin,
            state.MergingCommonBase);
        if (existing is null)
        {
            state.Order.Add(statement.KeyName);
        }
    }

    /// <summary>
    /// Resolves the rule actually applied. <see cref="XkbMergeMode.Default"/> inherits from the
    /// enclosing include, and <see cref="XkbMergeMode.Alternate"/> is approximated by
    /// <see cref="XkbMergeMode.Override"/> — it appears a handful of times in the whole corpus, and
    /// implementing alternate groups for those would buy nothing the model can hold.
    /// </summary>
    private static XkbMergeMode EffectiveMerge(XkbMergeMode declared, XkbMergeMode inherited, ResolutionState state)
    {
        switch (declared)
        {
            case XkbMergeMode.Default:
                return inherited == XkbMergeMode.Default ? XkbMergeMode.Override : inherited;

            case XkbMergeMode.Alternate:
                if (state.ReportedAlternate)
                {
                    return XkbMergeMode.Override;
                }

                state.ReportedAlternate = true;
                state.Diagnostics.Add(new LayoutImportDiagnostic(
                    ValidationSeverity.Info,
                    LayoutImportDiagnosticCodes.MergeModeApproximated,
                    "An 'alternate' composition rule was treated as 'override'."));
                return XkbMergeMode.Override;

            default:
                return declared;
        }
    }

    private static string FormatOrigin(string path, string section) =>
        $"{Path.GetFileName(path)}({section})";

    private static string FormatSpec(XkbIncludeSpec spec) =>
        spec.Section is null ? spec.File : $"{spec.File}({spec.Section})";

    /// <summary>Everything accumulated across one call to <see cref="Resolve"/>.</summary>
    private sealed class ResolutionState
    {
        public Dictionary<string, ResolvedXkbKey> Keys { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Key names in first-definition order. A dictionary's order is not part of its contract,
        /// and the importer's output must not depend on it.
        /// </summary>
        public List<string> Order { get; } = [];

        public HashSet<(string Path, string Section)> Visiting { get; } = [];

        public HashSet<string> ReportedFiles { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Sections whose findings have already been reported. A section reached twice through
        /// different parents — a diamond in the include graph — lost what it lost once.
        /// </summary>
        public HashSet<(string Path, string Section)> ReportedSections { get; } = [];

        public List<string> Chain { get; } = [];

        public List<LayoutImportDiagnostic> Diagnostics { get; } = [];

        public string? DisplayName { get; set; }

        /// <summary>
        /// Whether the merge in progress is <see cref="XkbCommonBase"/> rather than the layout.
        /// Every key it defines is marked, because a key the base supplies is common to every
        /// layout and says nothing about this one.
        /// </summary>
        public bool MergingCommonBase { get; set; }

        /// <summary>
        /// Whether the 'alternate' approximation has been reported. One note per import says what
        /// the user needs to know; one per occurrence would bury the findings that name a key.
        /// </summary>
        public bool ReportedAlternate { get; set; }

        public IReadOnlyList<ResolvedXkbKey> OrderedKeys() =>
            [.. Order.Select(name => Keys[name])];
    }
}
