using System.Reflection;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App;

public static class ApplicationReleaseInfo
{
    public static string Version { get; } = ReadVersion();

    public static int ProjectSchemaVersion => KeyboardProjectSchema.CurrentVersion;

    public static int DocumentSchemaVersion => JsonKeyboardProjectDocumentStore.CurrentDocumentSchemaVersion;

    public static string DisplayVersion => $"KeyboardStudio {Version}";

    private static string ReadVersion()
    {
        var assembly = typeof(ApplicationReleaseInfo).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataSeparator = informationalVersion.IndexOf('+', StringComparison.Ordinal);
            return metadataSeparator < 0
                ? informationalVersion
                : informationalVersion[..metadataSeparator];
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
