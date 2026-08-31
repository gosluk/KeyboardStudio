using System.Text.RegularExpressions;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed partial class ApplicationXamlPresentationTests
{
    /// <summary>Attributes whose value paints something, and so must come from the theme.</summary>
    private static readonly string[] PresentationAttributes =
    [
        "Background",
        "BorderBrush",
        "Foreground",
        "Fill",
        "Stroke",
        "BoxShadow",
        "CaretBrush",
        "SelectionBrush",
        "SelectionForegroundBrush",
        "Color",
    ];

    /// <summary>
    /// Values a presentation attribute may hold without naming a colour: the absence of paint, and
    /// the two ways of inheriting one.
    /// </summary>
    private static readonly string[] ColourlessValues = ["Transparent", "none", "None", "{x:Null}"];

    [Fact]
    [Trait("Category", "Unit")]
    public void NoViewDefinesItsOwnColour()
    {
        var offenders = new List<string>();

        // An audit that finds nothing because it read nothing is worse than no audit.
        Assert.Contains(ApplicationXamlSource.ThemeResourcesName, ApplicationXamlSource.All.Keys);
        Assert.True(ApplicationXamlSource.All.Count >= 10, "The application's XAML was not embedded.");

        foreach (var (name, xaml) in ApplicationXamlSource.All)
        {
            if (name == ApplicationXamlSource.ThemeResourcesName)
            {
                continue;
            }

            foreach (Match match in HexColour().Matches(xaml))
            {
                offenders.Add($"{name}: {match.Value}");
            }

            foreach (Match match in PresentationAttribute().Matches(xaml))
            {
                var value = match.Groups["value"].Value;
                if (!PresentationAttributes.Contains(match.Groups["attribute"].Value, StringComparer.Ordinal))
                {
                    continue;
                }

                if (value.StartsWith("{DynamicResource ", StringComparison.Ordinal) ||
                    value.StartsWith("{TemplateBinding ", StringComparison.Ordinal) ||
                    value.StartsWith("{Binding ", StringComparison.Ordinal) ||
                    ColourlessValues.Contains(value, StringComparer.Ordinal))
                {
                    continue;
                }

                offenders.Add($"{name}: {match.Groups["attribute"].Value}=\"{value}\"");
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EveryReferencedResourceIsDefinedByTheApplication()
    {
        var defined = ApplicationThemeTokens.Required.ToHashSet(StringComparer.Ordinal);
        foreach (var xaml in ApplicationXamlSource.All.Values)
        {
            foreach (Match match in ResourceKey().Matches(xaml))
            {
                defined.Add(match.Groups["key"].Value);
            }
        }

        var referenced = new List<string>();
        var inspected = 0;
        foreach (var (name, xaml) in ApplicationXamlSource.All)
        {
            foreach (Match match in ResourceReference().Matches(xaml))
            {
                inspected++;
                var key = match.Groups["key"].Value;
                if (!defined.Contains(key))
                {
                    referenced.Add($"{name}: {key}");
                }
            }
        }

        Assert.True(inspected >= 80, $"Only {inspected} resource references were inspected.");

        // A key resolved from Fluent instead of from KeyboardStudio survives an Avalonia upgrade
        // only for as long as Fluent keeps that key and that colour.
        Assert.Empty(referenced);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ThemeDependentResourcesAreNeverResolvedStatically()
    {
        var offenders = new List<string>();

        foreach (var (name, xaml) in ApplicationXamlSource.All)
        {
            foreach (Match match in StaticReference().Matches(xaml))
            {
                var key = match.Groups["key"].Value;
                if (ApplicationThemeTokens.Required.Contains(key, StringComparer.Ordinal))
                {
                    offenders.Add($"{name}: {key}");
                }
            }
        }

        // StaticResource captures whichever variant happened to be active when the reference was
        // resolved, so a theme change would leave that one brush behind.
        Assert.Empty(offenders);
    }

    [GeneratedRegex(@"#[0-9A-Fa-f]{3,8}\b")]
    private static partial Regex HexColour();

    [GeneratedRegex(@"(?<attribute>[A-Za-z.]+)\s*=\s*""(?<value>[^""]*)""")]
    private static partial Regex PresentationAttribute();

    [GeneratedRegex(@"x:Key\s*=\s*""(?<key>[A-Za-z][A-Za-z0-9_]*)""")]
    private static partial Regex ResourceKey();

    [GeneratedRegex(@"\{(?:Dynamic|Static)Resource\s+(?<key>[A-Za-z][A-Za-z0-9_]*)\s*\}")]
    private static partial Regex ResourceReference();

    [GeneratedRegex(@"\{StaticResource\s+(?<key>[A-Za-z][A-Za-z0-9_]*)\s*\}")]
    private static partial Regex StaticReference();
}
