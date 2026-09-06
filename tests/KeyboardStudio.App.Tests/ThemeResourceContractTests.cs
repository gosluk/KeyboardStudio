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
        // The dictionaries share their keys, not their values: reusing any token value across two
        // palettes means one theme has quietly borrowed another's rendering decision.
        var values = ThemeDictionaryValues();

        var reused = ApplicationThemeTokens.Required
            .Where(token => token is not "AccentForegroundBrush")
            .Where(token => values.Values.Select(theme => theme[token]).Distinct(StringComparer.Ordinal).Count() != values.Count)
            .ToList();

        Assert.Empty(reused);
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
                Assert.True(entries.TryAdd(key, CanonicalResourceValue(entry)), $"{variant} defines {key} more than once.");
            }

            result.Add(variant, entries);
        }

        return result;
    }

    private static string CanonicalResourceValue(XElement entry) =>
        CanonicalElement(entry, omitResourceKey: true);

    private static string CanonicalElement(XElement element, bool omitResourceKey = false)
    {
        var attributes = element.Attributes()
            .Where(attribute => !omitResourceKey || attribute.Name != Xaml + "Key")
            .OrderBy(attribute => attribute.Name.NamespaceName, StringComparer.Ordinal)
            .ThenBy(attribute => attribute.Name.LocalName, StringComparer.Ordinal)
            .Select(attribute => $"{attribute.Name}={NormalizeWhitespace(attribute.Value)}");

        var text = NormalizeWhitespace(string.Concat(element.Nodes().OfType<XText>().Select(node => node.Value)));
        var children = element.Elements().Select(child => CanonicalElement(child));

        return $"{element.Name}[{string.Join(";", attributes)}]{{{text}}}({string.Join(",", children)})";
    }

    private static string NormalizeWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
