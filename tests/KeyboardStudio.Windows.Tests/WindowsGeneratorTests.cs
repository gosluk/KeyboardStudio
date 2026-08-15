using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsGeneratorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Generate_WhenInputIsIdentical_ProducesIdenticalUnicodeMappingSource()
    {
        var project = DemoProjectFactory.Create();
        new KeyboardEditor(project).MapCharacter("KeyA", ModifierLayer.AltGr, "ą");
        var metadata = new WindowsLayoutMetadata("kbd-demo", "Demo layout");
        var generator = new WindowsArtifactGenerator(metadata);
        var options = new BuildOptions(BuildTarget.WindowsX64, "out");

        var first = await generator.GenerateAsync(project, options);
        var second = await generator.GenerateAsync(project, options);

        Assert.Equal(
            ["keyboard.c", "keyboard.def", "keyboard.h", "keyboard.rc"],
            first.Source.Files.Keys);
        Assert.Equal(first.Source.Files.Keys, second.Source.Files.Keys);
        Assert.All(first.Source.Files, file => Assert.Equal(file.Value, second.Source.Files[file.Key]));
        Assert.All(first.Source.Files.Values, source => Assert.DoesNotContain('\r', source));

        var firstSource = first.Source.Files["keyboard.c"];
        var secondSource = second.Source.Files["keyboard.c"];
        Assert.Equal(firstSource, secondSource);
        Assert.Contains("Layout ID: kbd-demo", firstSource);
        Assert.Contains("Layout name: Demo layout", firstSource);
        Assert.Contains("0x0105", firstSource);
        Assert.Contains("KbdLayerDescriptor", firstSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Generate_WhenLayoutHasNormalAndExtendedKeys_EmitsNativeScanCodeTables()
    {
        var project = DemoProjectFactory.Create();
        project.Keyboard.Keys.Add(new PhysicalKey
        {
            Id = "ArrowLeft",
            ScanCode = 0x4B,
            Extended = true
        });
        project.Layout.Mappings.Add(new KeyMapping
        {
            KeyId = "ArrowLeft",
            LogicalKey = LogicalKey.ArrowLeft
        });

        var artifact = await new WindowsArtifactGenerator(
                new WindowsLayoutMetadata("kbd-demo", "Demo layout"))
            .GenerateAsync(project, new BuildOptions(BuildTarget.WindowsX64, "out"));
        var source = artifact.Source.Files["keyboard.c"];

        Assert.Contains("static ALLOC_SECTION_LDATA USHORT ausVscToVk[]", source);
        Assert.Contains("0x0041 /* 0x1E */", source);
        Assert.Contains("{ 0x4B, 0x0025 | KBDEXT }", source);
        Assert.Contains("static ALLOC_SECTION_LDATA VSC_VK aE1VscToVk[]", source);
        Assert.Contains("{ 0x00, 0x0000 }", source);
        Assert.Contains("static ALLOC_SECTION_LDATA VSC_LPWSTR aKeyNamesExt[]", source);
        Assert.Contains("{ 0x4B, L\"Left\" }", source);
        Assert.DoesNotContain("L\"ArrowLeft\"", source);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Generate_WhenPrimaryTableContainsSpecialKeys_EmitsRequiredKbdFlags()
    {
        var project = DemoProjectFactory.Create();
        project.Keyboard.Keys.AddRange(
        [
            new PhysicalKey { Id = "RightShift", ScanCode = 0x36 },
            new PhysicalKey { Id = "NumLock", ScanCode = 0x45 },
            new PhysicalKey { Id = "Numpad1", ScanCode = 0x4F }
        ]);
        project.Layout.Mappings.AddRange(
        [
            new KeyMapping { KeyId = "RightShift", LogicalKey = LogicalKey.RightShift },
            new KeyMapping { KeyId = "NumLock", LogicalKey = LogicalKey.NumLock },
            new KeyMapping { KeyId = "Numpad1", LogicalKey = LogicalKey.Numpad1 }
        ]);

        var artifact = await new WindowsArtifactGenerator(
                new WindowsLayoutMetadata("kbd-demo", "Demo layout"))
            .GenerateAsync(project, new BuildOptions(BuildTarget.WindowsX64, "out"));
        var source = artifact.Source.Files["keyboard.c"];

        Assert.Contains("0x00A1 | KBDEXT /* 0x36 */", source);
        Assert.Contains("0x0090 | KBDEXT | KBDMULTIVK /* 0x45 */", source);
        Assert.Contains("0x0061 | KBDNUMPAD | KBDSPECIAL /* 0x4F */", source);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Generate_EmitsNativeModifierAndAltGrTables()
    {
        var artifact = await new WindowsArtifactGenerator(
                new WindowsLayoutMetadata("kbd-demo", "Demo layout"))
            .GenerateAsync(
                DemoProjectFactory.Create(),
                new BuildOptions(BuildTarget.WindowsX64, "out"));
        var source = artifact.Source.Files["keyboard.c"];

        Assert.Contains("static ALLOC_SECTION_LDATA VK_TO_BIT aVkToBits[]", source);
        Assert.Contains("{ VK_SHIFT, KBDSHIFT }", source);
        Assert.Contains("static ALLOC_SECTION_LDATA MODIFIERS CharModifiers", source);
        Assert.Equal(4, source.Split("SHFT_INVALID", StringSplitOptions.None).Length - 1);
        Assert.Contains("2 /* Control, Alt */", source);
        Assert.Contains("3 /* Shift, Control, Alt */", source);
        Assert.Contains("#define KEYBOARD_STUDIO_LOCALE_FLAGS KLLF_ALTGR", source);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Generate_WhenAltGrIsUsed_EmitsFourColumnNativeCharacterTable()
    {
        var project = DemoProjectFactory.Create();
        new KeyboardEditor(project).MapCharacter("KeyA", ModifierLayer.AltGr, "ą");

        var artifact = await new WindowsArtifactGenerator(
                new WindowsLayoutMetadata("kbd-demo", "Demo layout"))
            .GenerateAsync(project, new BuildOptions(BuildTarget.WindowsX64, "out"));
        var source = artifact.Source.Files["keyboard.c"];

        Assert.Contains("static ALLOC_SECTION_LDATA VK_TO_WCHARS4 aVkToWch4[]", source);
        Assert.Contains("{ 0x0041, CAPLOK, 0x0061, 0x0041, 0x0105, WCH_NONE }", source);
        Assert.Contains("{ 0x0000, 0, 0x0000, 0x0000, 0x0000, 0x0000 }", source);
        Assert.Contains("{ (PVK_TO_WCHARS1)aVkToWch4, 4, sizeof(aVkToWch4[0]) }", source);
        Assert.Contains("{ NULL, 0, 0 }", source);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Generate_EmitsCompleteMvpKeyboardTablesDescriptor()
    {
        var artifact = await new WindowsArtifactGenerator(
                new WindowsLayoutMetadata("kbd-demo", "Demo layout"))
            .GenerateAsync(
                DemoProjectFactory.Create(),
                new BuildOptions(BuildTarget.WindowsX64, "out"));
        var source = artifact.Source.Files["keyboard.c"];

        Assert.Contains("static ALLOC_SECTION_LDATA KBDTABLES KbdTables", source);
        Assert.Contains("&CharModifiers,", source);
        Assert.Contains("aVkToWcharTable,", source);
        Assert.Contains("sizeof(ausVscToVk) / sizeof(ausVscToVk[0]),", source);
        Assert.Contains("MAKELONG(KEYBOARD_STUDIO_LOCALE_FLAGS, KBD_VERSION),", source);
        Assert.Contains("NULL /* ligatures */", source);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Generate_ExportsTypedLayerDescriptorReturningKeyboardTables()
    {
        var artifact = await new WindowsArtifactGenerator(
                new WindowsLayoutMetadata("kbd-demo", "Demo layout"))
            .GenerateAsync(
                DemoProjectFactory.Create(),
                new BuildOptions(BuildTarget.WindowsX64, "out"));

        Assert.Contains("PKBDTABLES KbdLayerDescriptor(VOID)", artifact.Source.Files["keyboard.h"]);
        Assert.Contains("PKBDTABLES KbdLayerDescriptor(VOID)", artifact.Source.Files["keyboard.c"]);
        Assert.Contains("return &KbdTables;", artifact.Source.Files["keyboard.c"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Generate_EmitsDefinitionExportAndDeterministicVersionResource()
    {
        var artifact = await new WindowsArtifactGenerator(
                new WindowsLayoutMetadata("kbd-demo", "Demo \"Unicode\" layout", "2.3.4.5", "Example Co"))
            .GenerateAsync(
                DemoProjectFactory.Create(),
                new BuildOptions(BuildTarget.WindowsX64, "out"));

        Assert.Equal(
            "LIBRARY KBD_DEMO\n\nEXPORTS\n    KbdLayerDescriptor @1\n",
            artifact.Source.Files["keyboard.def"]);
        var resource = artifact.Source.Files["keyboard.rc"];
        Assert.Contains("FILEVERSION 2,3,4,5", resource);
        Assert.Contains("FILESUBTYPE VFT2_DRV_KEYBOARD", resource);
        Assert.Contains("VALUE \"FileDescription\", \"Demo \\\"Unicode\\\" layout\\0\"", resource);
        Assert.Contains("VALUE \"OriginalFilename\", \"kbd-demo.dll\\0\"", resource);
    }
}
