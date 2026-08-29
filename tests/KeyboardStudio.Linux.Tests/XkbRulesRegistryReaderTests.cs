using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbRulesRegistryReaderTests
{
    private static readonly XkbDataRoot Root = new("/usr/share/X11/xkb", LayoutSourceOrigin.System);

    private const string TwoLayouts = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xkbConfigRegistry version="1.1">
          <layoutList>
            <layout>
              <configItem>
                <name>us</name>
                <shortDescription>en</shortDescription>
                <description>English (US)</description>
                <languageList><iso639Id>eng</iso639Id></languageList>
                <countryList><iso3166Id>US</iso3166Id></countryList>
              </configItem>
              <variantList>
                <variant>
                  <configItem>
                    <name>dvorak</name>
                    <description>English (Dvorak)</description>
                  </configItem>
                </variant>
              </variantList>
            </layout>
            <layout>
              <configItem>
                <name>pl</name>
                <description>Polish</description>
                <languageList><iso639Id>pol</iso639Id></languageList>
                <countryList><iso3166Id>PL</iso3166Id></countryList>
              </configItem>
              <variantList>
                <variant>
                  <configItem>
                    <name>legacy</name>
                    <description>Polish (legacy)</description>
                    <languageList><iso639Id>szl</iso639Id></languageList>
                  </configItem>
                </variant>
              </variantList>
            </layout>
          </layoutList>
        </xkbConfigRegistry>
        """;

    private static XkbRulesRegistryReader ReaderOver(FakeXkbFileSystem fileSystem) =>
        new XkbRulesRegistryReader(fileSystem);

    private static FakeXkbFileSystem WithRegistry(string fileName, string content) =>
        new FakeXkbFileSystem().AddFile($"/usr/share/X11/xkb/rules/{fileName}", content);

    [Fact]
    [Trait("Category", "Unit")]
    public void Read_ForEachLayout_YieldsTheLayoutItselfBeforeItsVariants()
    {
        // The bare layout is importable in its own right: it resolves to the symbols file's
        // `default` section.
        var entries = ReaderOver(WithRegistry("evdev.xml", TwoLayouts)).Read(Root);

        Assert.Equal(
            [("us", null), ("us", "dvorak"), ("pl", null), ("pl", "legacy")],
            entries.Select(entry => (entry.LayoutId, entry.VariantId)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Read_ForALayout_CarriesTheDescriptionsAndCodesThatMakeItSearchable()
    {
        var entries = ReaderOver(WithRegistry("evdev.xml", TwoLayouts)).Read(Root);

        var us = entries.Single(entry => entry is { LayoutId: "us", VariantId: null });
        Assert.Equal("English (US)", us.DisplayName);
        Assert.Equal("en", us.ShortDescription);
        Assert.Equal(["eng"], us.Languages);
        Assert.Equal(["US"], us.Countries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Read_ForAVariantThatNamesNoLanguages_InheritsItsLayouts()
    {
        // Most variants say nothing about language or country. Without inheritance, searching for
        // "English" would find "English (US)" but not "English (Dvorak)".
        var entries = ReaderOver(WithRegistry("evdev.xml", TwoLayouts)).Read(Root);

        var dvorak = entries.Single(entry => entry.VariantId == "dvorak");
        Assert.Equal(["eng"], dvorak.Languages);
        Assert.Equal(["US"], dvorak.Countries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Read_ForAVariantThatNamesItsOwnLanguages_KeepsThemInsteadOfInheriting()
    {
        var entries = ReaderOver(WithRegistry("evdev.xml", TwoLayouts)).Read(Root);

        var legacy = entries.Single(entry => entry.VariantId == "legacy");
        Assert.Equal(["szl"], legacy.Languages);
        Assert.Equal(["PL"], legacy.Countries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Read_WhenTheExtrasFileIsPresent_AppendsItsLayouts()
    {
        var fileSystem = WithRegistry("evdev.xml", TwoLayouts)
            .AddFile("/usr/share/X11/xkb/rules/evdev.extras.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <xkbConfigRegistry version="1.1">
                  <layoutList>
                    <layout>
                      <configItem><name>brai</name><description>Braille</description></configItem>
                    </layout>
                  </layoutList>
                </xkbConfigRegistry>
                """);

        var entries = ReaderOver(fileSystem).Read(Root);

        Assert.Equal("Braille", entries.Single(entry => entry.LayoutId == "brai").DisplayName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Read_WhenBothFilesDescribeTheSameName_KeepsTheBaseRegistrysDescription()
    {
        var fileSystem = WithRegistry("evdev.xml", TwoLayouts)
            .AddFile("/usr/share/X11/xkb/rules/evdev.extras.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <xkbConfigRegistry version="1.1">
                  <layoutList>
                    <layout>
                      <configItem><name>us</name><description>Something else</description></configItem>
                    </layout>
                  </layoutList>
                </xkbConfigRegistry>
                """);

        var entries = ReaderOver(fileSystem).Read(Root);

        Assert.Equal("English (US)", entries.Single(entry => entry is { LayoutId: "us", VariantId: null }).DisplayName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Read_ForARegistryDeclaringADoctype_ParsesItWithoutFetchingTheDtd()
    {
        // The real evdev.xml declares SYSTEM "xkb.dtd". A resolver would try to fetch it from a
        // path the application does not own, so the reader must ignore the declaration outright.
        var fileSystem = WithRegistry("evdev.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE xkbConfigRegistry SYSTEM "/nonexistent/xkb.dtd">
            <xkbConfigRegistry version="1.1">
              <layoutList>
                <layout>
                  <configItem><name>us</name><description>English (US)</description></configItem>
                </layout>
              </layoutList>
            </xkbConfigRegistry>
            """);

        var entries = ReaderOver(fileSystem).Read(Root);

        Assert.Equal("English (US)", Assert.Single(entries).DisplayName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Read_ForALayoutWithNoDescription_FallsBackToItsName()
    {
        var fileSystem = WithRegistry("evdev.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <xkbConfigRegistry version="1.1">
              <layoutList>
                <layout><configItem><name>custom</name></configItem></layout>
              </layoutList>
            </xkbConfigRegistry>
            """);

        var entry = Assert.Single(ReaderOver(fileSystem).Read(Root));

        Assert.Equal("custom", entry.DisplayName);
        Assert.Null(entry.ShortDescription);
        Assert.Empty(entry.Languages);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Read_ForAnEntryWithNoName_SkipsItRatherThanInventingAnIdentifier()
    {
        // Without a name there is nothing to import: the identifier is what addresses the symbols.
        var fileSystem = WithRegistry("evdev.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <xkbConfigRegistry version="1.1">
              <layoutList>
                <layout><configItem><description>Nameless</description></configItem></layout>
                <layout>
                  <configItem><name>us</name><description>English (US)</description></configItem>
                  <variantList>
                    <variant><configItem><description>Also nameless</description></configItem></variant>
                  </variantList>
                </layout>
              </layoutList>
            </xkbConfigRegistry>
            """);

        var entries = ReaderOver(fileSystem).Read(Root);

        Assert.Equal([("us", null)], entries.Select(entry => (entry.LayoutId, entry.VariantId)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Read_ForARootWithNoRegistryFiles_ReturnsNothingRatherThanFailing()
    {
        // Roots legitimately carry symbols without rules; those layouts are listed from the
        // symbols directory instead.
        var entries = ReaderOver(new FakeXkbFileSystem().AddDirectory("/usr/share/X11/xkb")).Read(Root);

        Assert.Empty(entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Read_ForAMalformedRegistry_FailsRatherThanListingNothing()
    {
        // Silently returning an empty list would leave the user hunting for a layout they can see
        // is installed, with nothing to explain its absence.
        var fileSystem = WithRegistry("evdev.xml", "<xkbConfigRegistry><layoutList>");

        Assert.ThrowsAny<System.Xml.XmlException>(() => ReaderOver(fileSystem).Read(Root));
    }
}
