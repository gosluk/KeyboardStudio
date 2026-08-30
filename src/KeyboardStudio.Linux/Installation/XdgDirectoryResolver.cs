namespace KeyboardStudio.Linux;

/// <summary>Resolves XDG configuration/state homes without creating them.</summary>
public sealed class XdgDirectoryResolver
{
    private readonly IXkbEnvironment _environment;

    public XdgDirectoryResolver(IXkbEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public XdgDirectoryResolutionResult Resolve()
    {
        var diagnostics = new List<XkbDiagnostic>();
        var home = _environment.GetVariable("HOME");
        var config = ResolveHome("XDG_CONFIG_HOME", home, ".config", diagnostics);
        var state = ResolveHome(
            "XDG_STATE_HOME",
            home,
            Path.Combine(".local", "state"),
            diagnostics);
        if (config is null || state is null)
        {
            return new XdgDirectoryResolutionResult(false, null, diagnostics.AsReadOnly());
        }

        var paths = new XdgDirectoryPaths(
            config,
            state,
            Path.Combine(config, "xkb"),
            Path.Combine(state, "keyboardstudio", "xkb"));
        if (!IsSafe(paths.UserXkbRoot) || !IsSafe(paths.KeyboardStudioStateRoot))
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSC002",
                "The effective XDG configuration or state path is unsafe."));
            return new XdgDirectoryResolutionResult(false, null, diagnostics.AsReadOnly());
        }

        return new XdgDirectoryResolutionResult(true, paths, diagnostics.AsReadOnly());
    }

    private string? ResolveHome(
        string variable,
        string? home,
        string fallback,
        List<XkbDiagnostic> diagnostics)
    {
        var configured = _environment.GetVariable(variable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (Path.IsPathFullyQualified(configured))
            {
                return Path.GetFullPath(configured);
            }

            diagnostics.Add(new XkbDiagnostic(
                "KSC002",
                $"{variable} is relative and cannot be used safely."));
            return null;
        }

        if (!string.IsNullOrWhiteSpace(home) && Path.IsPathFullyQualified(home))
        {
            return Path.GetFullPath(Path.Combine(home, fallback));
        }

        diagnostics.Add(new XkbDiagnostic(
            "KSC002",
            $"Neither an absolute {variable} nor an absolute HOME is available."));
        return null;
    }

    private static bool IsSafe(string path) =>
        Path.IsPathFullyQualified(path) &&
        !string.Equals(Path.GetPathRoot(path), path, StringComparison.Ordinal) &&
        !path.StartsWith("/usr/", StringComparison.Ordinal) &&
        !path.StartsWith("/etc/", StringComparison.Ordinal);
}
