using System.Collections.Frozen;
using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Resolves XKB key names onto a template's physical keys by inverting
/// <see cref="IXkbKeyNameMapper"/>'s table, so that generation and import cannot disagree about
/// which key is which.
///
/// Inverting is not quite enough on its own, because a symbols file may write any of a key's names
/// and the table holds only the one generation writes. <c>keycodes/evdev</c> declares
/// <c>&lt;AC12&gt;</c> and <c>&lt;BKSL&gt;</c> to be one key, and eleven layouts write the first;
/// resolving only the second would drop the backslash key from every one of them. The alias pairs
/// below are therefore folded into the inverse table, and because an alias is a statement that two
/// names share a keycode rather than a redirection, they are folded in both directions and to a
/// fixed point: <c>&lt;I135&gt;</c> reaches <c>&lt;MENU&gt;</c> only through <c>&lt;COMP&gt;</c>.
/// </summary>
public sealed class XkbKeyNameResolver : IXkbKeyNameResolver
{
    /// <summary>
    /// Every alias <c>keycodes/evdev</c> declares, transcribed in full rather than filtered down to
    /// the ones that currently land on a template key. Most name media and vendor keys no template
    /// has, and those simply never enter the inverse table — but keeping the list whole is what
    /// lets it be diffed against the host's own file, which
    /// <c>XkbKeyNameResolverCorpusTests</c> does. Only evdev is transcribed: it is the keycodes
    /// file every current distribution loads, and the Sun and Macintosh files alias keys that exist
    /// on neither of this application's templates.
    /// </summary>
    private static readonly (string First, string Second)[] KeycodeAliases =
    [
        ("<AC12>", "<BKSL>"),
        ("<ALGR>", "<RALT>"),
        ("<MENU>", "<COMP>"),
        ("<HZTG>", "<TLDE>"),
        ("<LMTA>", "<LWIN>"),
        ("<RMTA>", "<RWIN>"),
        ("<OUTP>", "<I235>"),
        ("<KITG>", "<I236>"),
        ("<KIDN>", "<I237>"),
        ("<KIUP>", "<I238>"),
        ("<I121>", "<MUTE>"),
        ("<I122>", "<VOL->"),
        ("<I123>", "<VOL+>"),
        ("<I124>", "<POWR>"),
        ("<I125>", "<KPEQ>"),
        ("<I127>", "<PAUS>"),
        ("<I130>", "<HNGL>"),
        ("<I131>", "<HJCV>"),
        ("<I132>", "<AE13>"),
        ("<I133>", "<LWIN>"),
        ("<I134>", "<RWIN>"),
        ("<I135>", "<COMP>"),
        ("<I136>", "<STOP>"),
        ("<I137>", "<AGAI>"),
        ("<I138>", "<PROP>"),
        ("<I139>", "<UNDO>"),
        ("<I140>", "<FRNT>"),
        ("<I141>", "<COPY>"),
        ("<I142>", "<OPEN>"),
        ("<I143>", "<PAST>"),
        ("<I144>", "<FIND>"),
        ("<I145>", "<CUT>"),
        ("<I146>", "<HELP>"),
        ("<I191>", "<FK13>"),
        ("<I192>", "<FK14>"),
        ("<I193>", "<FK15>"),
        ("<I194>", "<FK16>"),
        ("<I195>", "<FK17>"),
        ("<I196>", "<FK18>"),
        ("<I197>", "<FK19>"),
        ("<I198>", "<FK20>"),
        ("<I199>", "<FK21>"),
        ("<I200>", "<FK22>"),
        ("<I201>", "<FK23>"),
        ("<I202>", "<FK24>"),
        ("<MDSW>", "<LVL5>"),
        ("<KPPT>", "<I129>")
    ];

    /// <summary>
    /// The letter rows each phonetic alias set names, in the order <c>keycodes/aliases</c> writes
    /// them: the top row, then the home row, then the bottom row. Spelling the sets out as three
    /// strings keeps them readable as what they are — three keyboards' worth of letter positions —
    /// and keeps the difference between them visible at a glance.
    /// </summary>
    private static readonly Dictionary<XkbKeyAliasSet, string[]> LatinAliasRows = new()
    {
        [XkbKeyAliasSet.Qwerty] = ["QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM"],
        [XkbKeyAliasSet.Azerty] = ["AZERTYUIOP", "QSDFGHJKLM", "WXCVBN"],
        [XkbKeyAliasSet.Qwertz] = ["QWERTZUIOP", "ASDFGHJKL", "YXCVBNM"]
    };

    /// <summary>
    /// The layouts <c>rules/evdev</c> reads with a set other than <c>qwerty</c>. Held here rather
    /// than parsed from the rules file, which is a format nothing else in the importer reads;
    /// <c>XkbKeyNameResolverCorpusTests</c> diffs both lists against the host's rules so that a
    /// distribution moving a country between them is caught rather than silently followed.
    /// </summary>
    private static readonly FrozenDictionary<string, XkbKeyAliasSet> AliasSetsByLayout =
        new Dictionary<string, XkbKeyAliasSet>(StringComparer.Ordinal)
        {
            ["be"] = XkbKeyAliasSet.Azerty,
            ["fr"] = XkbKeyAliasSet.Azerty,
            ["al"] = XkbKeyAliasSet.Qwertz,
            ["ch"] = XkbKeyAliasSet.Qwertz,
            ["cz"] = XkbKeyAliasSet.Qwertz,
            ["de"] = XkbKeyAliasSet.Qwertz,
            ["hr"] = XkbKeyAliasSet.Qwertz,
            ["hu"] = XkbKeyAliasSet.Qwertz,
            ["ro"] = XkbKeyAliasSet.Qwertz,
            ["si"] = XkbKeyAliasSet.Qwertz,
            ["sk"] = XkbKeyAliasSet.Qwertz
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private readonly IXkbKeyNameMapper _mapper;
    private readonly XkbKeyAliasSet _aliasSet;
    private readonly Dictionary<string, FrozenDictionary<string, string>> _byTemplate = new(StringComparer.Ordinal);

    public XkbKeyNameResolver()
        : this(new XkbKeyNameMapper())
    {
    }

    public XkbKeyNameResolver(IXkbKeyNameMapper mapper, XkbKeyAliasSet aliasSet = XkbKeyAliasSet.Qwerty)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _aliasSet = aliasSet;
    }

    /// <summary>
    /// The alias set the host would read a layout with, from the layout's registry name.
    ///
    /// The importer needs this before it can resolve a single key of a phonetic layout, and the
    /// answer belongs beside the alias tables it selects between rather than in the caller.
    /// </summary>
    /// <param name="layout">A registry layout name, such as <c>de</c> or <c>fr</c>.</param>
    public static XkbKeyAliasSet AliasSetForLayout(string layout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layout);

        return AliasSetsByLayout.TryGetValue(layout, out var aliasSet) ? aliasSet : XkbKeyAliasSet.Qwerty;
    }

    /// <inheritdoc />
    public XkbKeyNameResolveResult Resolve(string templateId, string keyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName);

        var name = keyName.Trim();
        if (GetInverse(templateId).TryGetValue(name, out var keyId))
        {
            return new XkbKeyNameResolveResult(keyId, null);
        }

        // No key id is attached: the diagnostic's key id is what the editor jumps to, and the whole
        // point of this finding is that there is no key here to jump to. The name goes in the
        // message instead, where it reads as the explanation it is.
        return new XkbKeyNameResolveResult(
            null,
            new LayoutImportDiagnostic(
                ValidationSeverity.Info,
                LayoutImportDiagnosticCodes.PhysicalKeyNotInTemplate,
                $"'{name}' is not a key of the '{templateId}' keyboard, so it was skipped."));
    }

    private FrozenDictionary<string, string> GetInverse(string templateId)
    {
        if (_byTemplate.TryGetValue(templateId, out var cached))
        {
            return cached;
        }

        var inverse = BuildInverse(_mapper.GetMappings(templateId), _aliasSet);
        _byTemplate[templateId] = inverse;
        return inverse;
    }

    /// <summary>
    /// Builds one template's key name to physical key table: the mapper's own names first, then the
    /// alias names that reach one of them.
    ///
    /// A name the table already holds is never overwritten, so the names generation writes always
    /// win over the names that merely reach them. Nothing in evdev currently aliases two of the
    /// same template's keys together, and a test holds it that way; were a distribution to add such
    /// an alias, taking the first is at least stable rather than dependent on the order the pairs
    /// happen to be listed in.
    /// </summary>
    private static FrozenDictionary<string, string> BuildInverse(
        IReadOnlyDictionary<string, string> mappings,
        XkbKeyAliasSet aliasSet)
    {
        var inverse = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (keyId, keyName) in mappings)
        {
            inverse.TryAdd(keyName, keyId);
        }

        if (inverse.Count == 0)
        {
            return FrozenDictionary<string, string>.Empty;
        }

        var aliases = KeycodeAliases.Concat(LatinAliases(aliasSet)).ToArray();

        // Repeated until nothing new appears, because an alias may reach a template key only
        // through another alias, and the pairs are in no particular order.
        bool added;
        do
        {
            added = false;
            foreach (var (first, second) in aliases)
            {
                added |= Link(inverse, first, second);
                added |= Link(inverse, second, first);
            }
        }
        while (added);

        return inverse.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static bool Link(Dictionary<string, string> inverse, string from, string to)
    {
        if (!inverse.TryGetValue(from, out var keyId))
        {
            return false;
        }

        return inverse.TryAdd(to, keyId);
    }

    private static IEnumerable<(string First, string Second)> LatinAliases(XkbKeyAliasSet aliasSet)
    {
        var rows = LatinAliasRows[aliasSet];

        for (var row = 0; row < rows.Length; row++)
        {
            // The rows an XKB key name counts from: AD is the top letter row, AC the home row, AB
            // the bottom one, and the number is the position along it.
            var prefix = row switch { 0 => "AD", 1 => "AC", _ => "AB" };

            for (var position = 0; position < rows[row].Length; position++)
            {
                yield return ($"<Lat{rows[row][position]}>", $"<{prefix}{position + 1:D2}>");
            }
        }
    }
}
