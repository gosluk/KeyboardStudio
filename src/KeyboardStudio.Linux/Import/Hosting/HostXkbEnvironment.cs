namespace KeyboardStudio.Linux;

/// <summary>Reads the real process environment.</summary>
public sealed class HostXkbEnvironment : IXkbEnvironment
{
    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);
}
