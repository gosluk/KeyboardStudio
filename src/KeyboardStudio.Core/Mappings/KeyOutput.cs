using System.Text.Json.Serialization;

namespace KeyboardStudio.Core;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CharacterOutput), "character")]
[JsonDerivedType(typeof(SpecialKeyOutput), "specialKey")]
[JsonDerivedType(typeof(NoOutput), "none")]
public abstract record KeyOutput;

public sealed record CharacterOutput(string Value) : KeyOutput;
public sealed record SpecialKeyOutput(LogicalKey Key) : KeyOutput;
public sealed record NoOutput() : KeyOutput;
