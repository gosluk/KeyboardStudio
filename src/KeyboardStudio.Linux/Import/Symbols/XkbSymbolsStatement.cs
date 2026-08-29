namespace KeyboardStudio.Linux;

/// <summary>
/// One statement inside an <c>xkb_symbols</c> section.
///
/// The hierarchy is closed to this assembly: it models exactly the XKB statements the reader has an
/// opinion about, and a new one is a change to the reader rather than something a caller supplies.
/// </summary>
public abstract record XkbSymbolsStatement
{
    private protected XkbSymbolsStatement()
    {
    }
}
