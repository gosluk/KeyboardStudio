using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class ValidationRuleTests
{
    [Fact]
    public void MetadataValidationRule_WhenRequiredFieldsAreEmpty_ReportsEachField()
    {
        var source = DemoProjectFactory.Create();
        var project = new KeyboardProject
        {
            Metadata = new ProjectMetadata
            {
                Name = string.Empty,
                Description = string.Empty,
                Version = string.Empty,
                Language = string.Empty
            },
            Keyboard = source.Keyboard,
            Layout = source.Layout
        };

        var issues = new MetadataValidationRule().Validate(project);

        Assert.Equal(["META001", "META002", "META003"], issues.Select(issue => issue.Code));
    }

    [Fact]
    public void PhysicalKeyboardValidationRule_WhenPhysicalIdentityIsInvalid_ReportsEachProblem()
    {
        var project = DemoProjectFactory.Create();
        project.Keyboard.Keys.Add(new PhysicalKey { Id = "KeyA", ScanCode = 0x1E });
        project.Keyboard.Keys.Add(new PhysicalKey { Id = "OutOfRange", ScanCode = 0x100 });

        var issues = new PhysicalKeyboardValidationRule().Validate(project);

        Assert.Contains(issues, issue => issue.Code == "KEY001" && issue.KeyId == "KeyA");
        Assert.Contains(issues, issue => issue.Code == "KEY002" && issue.KeyId == "OutOfRange");
        Assert.Contains(issues, issue => issue.Code == "KEY003");
    }

    [Fact]
    public void MappingValidationRule_WhenMappingsAreInvalid_ReportsEachProblem()
    {
        var project = DemoProjectFactory.Create();
        project.Layout.Mappings.Add(new KeyMapping
        {
            KeyId = "MissingKey",
            LogicalKey = LogicalKey.A
        });
        project.Layout.Mappings.Add(new KeyMapping
        {
            KeyId = "KeyA",
            LogicalKey = LogicalKey.A
        });
        project.Layout.Find("KeyB")!.Outputs[ModifierLayer.Default] = null!;

        var issues = new MappingValidationRule().Validate(project);

        Assert.Contains(issues, issue => issue.Code == "MAP001" && issue.KeyId == "MissingKey");
        Assert.Contains(issues, issue => issue.Code == "MAP002" && issue.KeyId == "KeyB");
        Assert.Contains(issues, issue => issue.Code == "MAP003" && issue.KeyId == "KeyA");
    }

    [Fact]
    public void KeyboardProjectValidator_WhenCustomRulesAreSupplied_ComposesTheirResults()
    {
        var validator = new KeyboardProjectValidator([
            new StaticValidationRule("TEST001"),
            new StaticValidationRule("TEST002")
        ]);

        var issues = validator.Validate(DemoProjectFactory.Create());

        Assert.Equal(["TEST001", "TEST002"], issues.Select(issue => issue.Code));
    }

    private sealed class StaticValidationRule(string code) : IKeyboardProjectValidationRule
    {
        public IReadOnlyList<ValidationIssue> Validate(KeyboardProject project) =>
            [new ValidationIssue(ValidationSeverity.Info, code, code)];
    }
}
