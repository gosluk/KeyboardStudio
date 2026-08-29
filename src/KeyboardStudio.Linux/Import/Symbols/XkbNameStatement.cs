namespace KeyboardStudio.Linux;

/// <summary>
/// <c>name[Group1] = "Polish"</c> — the layout's own name for a group.
/// </summary>
/// <param name="Group">One-based group index the name belongs to.</param>
/// <param name="Value">The name, which becomes the imported project's name and description.</param>
public sealed record XkbNameStatement(int Group, string Value) : XkbSymbolsStatement;
