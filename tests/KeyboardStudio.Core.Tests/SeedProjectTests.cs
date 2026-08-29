using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class SeedProjectTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_ForUsBasic_ReturnsAProjectThatIsNotEmpty()
    {
        var project = new EmbeddedSeedProjectSource().Create(SeedProjectId.UsBasic);

        Assert.Equal("iso-105", project.Keyboard.Id);
        Assert.NotEmpty(project.Layout.Mappings);
        Assert.All(project.Layout.Mappings, mapping => Assert.NotEqual(LogicalKey.None, mapping.LogicalKey));
        Assert.All(project.Layout.Mappings, mapping => Assert.NotEmpty(mapping.Outputs));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_ForUsBasic_MapsEveryPhysicalKey()
    {
        var project = new EmbeddedSeedProjectSource().Create(SeedProjectId.UsBasic);

        var mappedKeyIds = project.Layout.Mappings
            .Select(mapping => mapping.KeyId)
            .ToHashSet(StringComparer.Ordinal);
        var unmapped = project.Keyboard.Keys
            .Select(key => key.Id)
            .Where(id => !mappedKeyIds.Contains(id))
            .ToArray();

        Assert.Empty(unmapped);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_ForUsBasic_ProducesUsCharactersOnTheAlphanumericBlock()
    {
        var project = new EmbeddedSeedProjectSource().Create(SeedProjectId.UsBasic);

        AssertCharacters(project, "KeyA", "a", "A");
        AssertCharacters(project, "Digit1", "1", "!");
        AssertCharacters(project, "Semicolon", ";", ":");
        AssertCharacters(project, "Slash", "/", "?");

        // us(basic) defines <BKSL> as backslash/bar; <LSGT> is not defined there and falls
        // through to the pc default of less/greater on ISO hardware.
        AssertCharacters(project, "IntlHash", "\\", "|");
        AssertCharacters(project, "IntlBackslash", "<", ">");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_ForUsBasic_UsesSpecialKeyOutputsForNonCharacterKeys()
    {
        var project = new EmbeddedSeedProjectSource().Create(SeedProjectId.UsBasic);

        var enter = project.Layout.Find("Enter");
        Assert.NotNull(enter);
        Assert.Equal(new SpecialKeyOutput(LogicalKey.Enter), enter.Outputs[ModifierLayer.Default]);

        var space = project.Layout.Find("Space");
        Assert.NotNull(space);
        Assert.Equal(new SpecialKeyOutput(LogicalKey.Space), space.Outputs[ModifierLayer.Default]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_ForUsBasic_ValidatesWithoutWarningsOrErrors()
    {
        var project = new EmbeddedSeedProjectSource().Create(SeedProjectId.UsBasic);
        var validator = new KeyboardProjectValidator([
            new MetadataValidationRule(),
            new PhysicalKeyboardValidationRule(),
            new MappingValidationRule()
        ]);

        var issues = validator.Validate(project).Issues
            .Where(issue => issue.Severity != ValidationSeverity.Info)
            .ToArray();

        Assert.Empty(issues);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_ForUsBasic_MatchesTheIso105GeometryTemplate()
    {
        // The seed is generated from templates/iso-105.json by scripts/generate-us-basic-seed.py.
        // This is the drift guard: editing the geometry template without regenerating the seed
        // would otherwise ship two disagreeing copies of the same keyboard.
        var seed = new EmbeddedSeedProjectSource().Create(SeedProjectId.UsBasic);
        var template = new KeyboardTemplateProvider().Load("iso-105");

        Assert.Equal(template.Id, seed.Keyboard.Id);
        Assert.Equal(
            template.Keys.Select(Describe).ToArray(),
            seed.Keyboard.Keys.Select(Describe).ToArray());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WhenCalledTwice_ReturnsIndependentProjects()
    {
        var source = new EmbeddedSeedProjectSource();

        var first = source.Create(SeedProjectId.UsBasic);
        var second = source.Create(SeedProjectId.UsBasic);
        first.Layout.Find("KeyA")!.Outputs[ModifierLayer.Default] = new CharacterOutput("z");

        Assert.Equal(
            new CharacterOutput("a"),
            second.Layout.Find("KeyA")!.Outputs[ModifierLayer.Default]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WithAnUnknownSeedId_Throws()
    {
        var exception = Assert.Throws<SeedProjectException>(
            () => new EmbeddedSeedProjectSource().Create("no-such-seed"));

        Assert.Equal("no-such-seed", exception.SeedId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SeedIds_ContainsTheDefaultSeed()
    {
        Assert.Contains(SeedProjectId.Default, new EmbeddedSeedProjectSource().SeedIds);
    }

    private static void AssertCharacters(
        KeyboardProject project,
        string keyId,
        string expectedDefault,
        string expectedShift)
    {
        var mapping = project.Layout.Find(keyId);
        Assert.NotNull(mapping);
        Assert.Equal(new CharacterOutput(expectedDefault), mapping.Outputs[ModifierLayer.Default]);
        Assert.Equal(new CharacterOutput(expectedShift), mapping.Outputs[ModifierLayer.Shift]);
    }

    private static (string, int, bool, double, double, double, double) Describe(PhysicalKey key) =>
        (key.Id, key.ScanCode, key.Extended, key.X, key.Y, key.Width, key.Height);
}
