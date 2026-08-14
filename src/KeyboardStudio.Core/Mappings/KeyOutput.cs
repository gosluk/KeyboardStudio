namespace KeyboardStudio.Core;

public abstract record KeyOutput;

public sealed record CharacterOutput(string Value) : KeyOutput;
public sealed record SpecialKeyOutput(LogicalKey Key) : KeyOutput;
public sealed record NoOutput() : KeyOutput;
