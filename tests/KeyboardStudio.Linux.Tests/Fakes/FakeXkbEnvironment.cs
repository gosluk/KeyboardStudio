using KeyboardStudio.Linux;

namespace KeyboardStudio.Linux.Tests;

/// <summary>An environment built from a dictionary, so tests never mutate the test host's own.</summary>
public sealed class FakeXkbEnvironment : IXkbEnvironment
{
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

    public FakeXkbEnvironment Set(string name, string value)
    {
        _variables[name] = value;
        return this;
    }

    public string? GetVariable(string name) =>
        _variables.TryGetValue(name, out var value) ? value : null;
}
