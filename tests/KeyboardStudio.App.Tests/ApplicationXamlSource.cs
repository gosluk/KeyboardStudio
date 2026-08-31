using System.Collections.Immutable;
using System.Reflection;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// The application's shipped XAML, read from this assembly's embedded copy.
/// </summary>
/// <remarks>
/// The markup is embedded at build time rather than located relative to the test's working
/// directory, so these audits behave the same on a developer machine and on a runner.
/// </remarks>
internal static class ApplicationXamlSource
{
    private const string Prefix = "KeyboardStudio.App.Tests.ApplicationXaml.";

    public static string ThemeResourcesName => "Styles.ThemeResources.axaml";

    /// <summary>Every embedded application XAML file, keyed by its path below the project root.</summary>
    public static ImmutableSortedDictionary<string, string> All { get; } = Load();

    public static string Read(string name) => All[name];

    private static ImmutableSortedDictionary<string, string> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var builder = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Embedded resource '{resource}' could not be opened.");
            using var reader = new StreamReader(stream);
            builder.Add(resource[Prefix.Length..], reader.ReadToEnd());
        }

        return builder.ToImmutable();
    }
}
