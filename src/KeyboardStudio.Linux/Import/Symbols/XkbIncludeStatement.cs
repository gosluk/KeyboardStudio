namespace KeyboardStudio.Linux;

/// <summary>
/// <c>include "us(basic)"</c> — this section is composed on top of another one.
/// </summary>
/// <param name="Merge">The prefix that decides which definition wins when both describe a key.</param>
/// <param name="Specification">
/// The include string exactly as written. It is left uninterpreted here because a single string can
/// name several sections joined by <c>+</c> or <c>|</c>; splitting it belongs to the resolver, which
/// is the part that knows what the roots contain.
/// </param>
public sealed record XkbIncludeStatement(XkbMergeMode Merge, string Specification) : XkbSymbolsStatement;
