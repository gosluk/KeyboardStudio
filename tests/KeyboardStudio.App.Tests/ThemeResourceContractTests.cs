using System.Xml.Linq;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class ThemeResourceContractTests
{
    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    [Trait("Category", "Unit")]
    public void ThemeResources_DefineOneDictionaryPerApplicationTheme()
    {
        var variants = ThemeDictionaries().Keys;

        Assert.Equal(
            ["ApplicationThemeVariants.Black", "ApplicationThemeVariants.Gray", "ApplicationThemeVariants.White"],
            variants.Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EveryThemeDefinesExactlyTheRequiredTokens()
    {
        var required = ApplicationThemeTokens.Required.ToHashSet(StringComparer.Ordinal);

        foreach (var (variant, keys) in ThemeDictionaries())
        {
            var defined = keys.ToHashSet(StringComparer.Ordinal);

            Assert.Equal(keys.Count, defined.Count);
            Assert.Empty(required.Except(defined).Order(StringComparer.Ordinal));
            Assert.Empty(defined.Except(required).Order(StringComparer.Ordinal));
            Assert.NotEmpty(defined);
            Assert.True(defined.SetEquals(required), $"{variant} does not define the required token set.");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RequiredTokensAreDeclaredOnce()
    {
        var required = ApplicationThemeTokens.Required;

        Assert.Equal(required.Count, required.Distinct(StringComparer.Ordinal).Count());
        Assert.All(required, token => Assert.False(string.IsNullOrWhiteSpace(token)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EveryThemeGivesEachTokenItsOwnValue()
    {
        // The dictionaries share their keys, not their values: a token that resolved to the same
        // colour in all three would mean one palette had quietly borrowed another's.
        var values = ThemeDictionaryValues();

        var identical = ApplicationThemeTokens.Required
            .Where(token => values.Values.Select(theme => theme[token]).Distinct(StringComparer.Ordinal).Count() == 1)
            .Where(token => token is not "AccentForegroundBrush")
            .ToList();

        Assert.Empty(identical);
    }

    private static Dictionary<string, List<string>> ThemeDictionaries() =>
        ThemeDictionaryValues().ToDictionary(entry => entry.Key, entry => entry.Value.Keys.ToList(), StringComparer.Ordinal);

    private static Dictionary<string, Dictionary<string, string>> ThemeDictionaryValues()
    {
        var document = XDocument.Parse(ApplicationXamlSource.Read(ApplicationXamlSource.ThemeResourcesName));
        var dictionaries = document.Descendants(Avalonia + "ResourceDictionary.ThemeDictionaries").Single();
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var dictionary in dictionaries.Elements(Avalonia + "ResourceDictionary"))
        {
            var variant = dictionary.Attribute(Xaml + "Key")!.Value
                .Replace("{x:Static app:", string.Empty, StringComparison.Ordinal)
                .Replace("}", string.Empty, StringComparison.Ordinal);

            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in dictionary.Elements())
            {
                var key = entry.Attribute(Xaml + "Key")!.Value;
                entries[key] = entry.Attribute("Color")?.Value ?? entry.Value.Trim();
            }

            result.Add(variant, entries);
        }

        return result;
    }
}
