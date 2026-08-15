using System.Text;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class KeyboardTemplateProviderTests
{
    private static readonly KeyboardTemplateDescriptor TestDescriptor =
        new("test-layout", "Test layout", 1, 54, 4);

    private const string ValidTemplate = """
        {
          "schemaVersion": 1,
          "id": "test-layout",
          "name": "Test layout",
          "unitWidth": 54,
          "unitGap": 4,
          "keys": [
            {
              "id": "KeyA",
              "scanCode": 30,
              "x": 1.75,
              "y": 3,
              "width": 1,
              "height": 1
            }
          ]
        }
        """;

    [Fact]
    [Trait("Category", "Unit")]
    public void Templates_WhenDefaultProviderIsCreated_EnumeratesBuiltInTemplates()
    {
        var provider = new KeyboardTemplateProvider();

        Assert.Collection(
            provider.Templates,
            iso =>
            {
                Assert.Equal("iso-105", iso.Id);
                Assert.Equal("ISO 105-key", iso.Name);
                Assert.Equal(105, iso.ExpectedKeyCount);
                Assert.Equal(54, iso.UnitWidth);
                Assert.Equal(4, iso.UnitGap);
            },
            ansi =>
            {
                Assert.Equal("ansi-104", ansi.Id);
                Assert.Equal("ANSI 104-key", ansi.Name);
                Assert.Equal(104, ansi.ExpectedKeyCount);
                Assert.Equal(54, ansi.UnitWidth);
                Assert.Equal(4, ansi.UnitGap);
            });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OpenRead_WhenBuiltInTemplateIsRequested_ReturnsEmbeddedTemplateResource()
    {
        var source = new EmbeddedKeyboardTemplateContentSource();

        using var stream = source.OpenRead("iso-105");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        Assert.Contains("\"id\": \"iso-105\"", json);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Load_WhenTemplateIsValid_ConvertsGeometryToPhysicalKeyboard()
    {
        var source = new DictionaryTemplateContentSource(ValidTemplate);
        var provider = new KeyboardTemplateProvider(source, [TestDescriptor]);

        var keyboard = provider.Load("test-layout");

        Assert.Equal("test-layout", keyboard.Id);
        var key = Assert.Single(keyboard.Keys);
        Assert.Equal("KeyA", key.Id);
        Assert.Equal(30, key.ScanCode);
        Assert.False(key.Extended);
        Assert.Equal(1.75, key.X);
        Assert.Equal(3, key.Y);
        Assert.Equal(1, key.Width);
        Assert.Equal(1, key.Height);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Load_WhenTemplateIsLoadedRepeatedly_CachesValidatedTemplateAndReturnsDefensiveLists()
    {
        var source = new DictionaryTemplateContentSource(ValidTemplate);
        var provider = new KeyboardTemplateProvider(source, [TestDescriptor]);

        var first = provider.Load("test-layout");
        var second = provider.Load("test-layout");
        first.Keys.Clear();
        var third = provider.Load("test-layout");

        Assert.Equal(1, source.OpenCount);
        Assert.NotSame(first, second);
        Assert.NotSame(first.Keys, second.Keys);
        Assert.Single(second.Keys);
        Assert.Single(third.Keys);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Load_WhenTemplateIdIsUnknown_ReportsUnknownTemplateWithoutOpeningSource()
    {
        var source = new DictionaryTemplateContentSource(ValidTemplate);
        var provider = new KeyboardTemplateProvider(source, [TestDescriptor]);

        var exception = Assert.Throws<KeyboardTemplateException>(() => provider.Load("missing-layout"));

        Assert.Equal(KeyboardTemplateErrorCode.UnknownTemplate, exception.Code);
        Assert.Equal("missing-layout", exception.TemplateId);
        Assert.Equal(0, source.OpenCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Load_WhenSchemaVersionIsUnsupported_ReportsUnsupportedSchemaVersion()
    {
        var source = new DictionaryTemplateContentSource(
            ValidTemplate.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal));
        var provider = new KeyboardTemplateProvider(source, [TestDescriptor]);

        var exception = Assert.Throws<KeyboardTemplateException>(() => provider.Load("test-layout"));

        Assert.Equal(KeyboardTemplateErrorCode.UnsupportedSchemaVersion, exception.Code);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Load_WhenPhysicalKeyIdIsDuplicated_ReportsDuplicateKeyId()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "id": "test-layout",
              "name": "Test layout",
              "unitWidth": 54,
              "unitGap": 4,
              "keys": [
                { "id": "KeyA", "scanCode": 30, "x": 0, "y": 0, "width": 1, "height": 1 },
                { "id": "KeyA", "scanCode": 31, "x": 1, "y": 0, "width": 1, "height": 1 }
              ]
            }
            """;
        var source = new DictionaryTemplateContentSource(json);
        var provider = new KeyboardTemplateProvider(
            source,
            [TestDescriptor with { ExpectedKeyCount = 2 }]);

        var exception = Assert.Throws<KeyboardTemplateException>(() => provider.Load("test-layout"));

        Assert.Equal(KeyboardTemplateErrorCode.DuplicateKeyId, exception.Code);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Load_WhenScanCodeIdentityIsDuplicated_ReportsDuplicateScanCodeIdentity()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "id": "test-layout",
              "name": "Test layout",
              "unitWidth": 54,
              "unitGap": 4,
              "keys": [
                { "id": "KeyA", "scanCode": 30, "x": 0, "y": 0, "width": 1, "height": 1 },
                { "id": "KeyB", "scanCode": 30, "x": 1, "y": 0, "width": 1, "height": 1 }
              ]
            }
            """;
        var source = new DictionaryTemplateContentSource(json);
        var provider = new KeyboardTemplateProvider(
            source,
            [TestDescriptor with { ExpectedKeyCount = 2 }]);

        var exception = Assert.Throws<KeyboardTemplateException>(() => provider.Load("test-layout"));

        Assert.Equal(KeyboardTemplateErrorCode.DuplicateScanCodeIdentity, exception.Code);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Load_WhenScanCodeMatchesButExtendedIdentityDiffers_AllowsBothKeys()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "id": "test-layout",
              "name": "Test layout",
              "unitWidth": 54,
              "unitGap": 4,
              "keys": [
                { "id": "KeyA", "scanCode": 30, "x": 0, "y": 0, "width": 1, "height": 1 },
                { "id": "KeyB", "scanCode": 30, "extended": true, "x": 1, "y": 0, "width": 1, "height": 1 }
              ]
            }
            """;
        var source = new DictionaryTemplateContentSource(json);
        var provider = new KeyboardTemplateProvider(
            source,
            [TestDescriptor with { ExpectedKeyCount = 2 }]);

        var keyboard = provider.Load("test-layout");

        Assert.Equal(2, keyboard.Keys.Count);
        Assert.False(keyboard.Keys[0].Extended);
        Assert.True(keyboard.Keys[1].Extended);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Load_WhenTemplateHasWrongKeyCount_ReportsIncompleteTemplate()
    {
        var source = new DictionaryTemplateContentSource(ValidTemplate);
        var provider = new KeyboardTemplateProvider(
            source,
            [TestDescriptor with { ExpectedKeyCount = 2 }]);

        var exception = Assert.Throws<KeyboardTemplateException>(() => provider.Load("test-layout"));

        Assert.Equal(KeyboardTemplateErrorCode.IncompleteTemplate, exception.Code);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Load_WhenUnknownJsonPropertyExists_ReportsInvalidJson()
    {
        var source = new DictionaryTemplateContentSource(
            ValidTemplate.Replace("\"unitGap\": 4,", "\"unitGap\": 4,\n  \"unexpected\": true,", StringComparison.Ordinal));
        var provider = new KeyboardTemplateProvider(source, [TestDescriptor]);

        var exception = Assert.Throws<KeyboardTemplateException>(() => provider.Load("test-layout"));

        Assert.Equal(KeyboardTemplateErrorCode.InvalidJson, exception.Code);
    }

    private sealed class DictionaryTemplateContentSource : IKeyboardTemplateContentSource
    {
        private readonly string _json;

        public DictionaryTemplateContentSource(string json)
        {
            _json = json;
        }

        public int OpenCount { get; private set; }

        public Stream OpenRead(string templateId)
        {
            OpenCount++;
            if (!string.Equals(templateId, TestDescriptor.Id, StringComparison.Ordinal))
            {
                throw new FileNotFoundException($"Template '{templateId}' was not found.");
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(_json), writable: false);
        }
    }
}
