using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class ValidationRuleTests
{
    [Fact]
    public void KeyboardProjectDiagnosticCodes_WhenRead_HaveStableValues()
    {
        Assert.Equal("KSP001", KeyboardProjectDiagnosticCodes.DuplicatePhysicalKeyId);
        Assert.Equal("KSP002", KeyboardProjectDiagnosticCodes.InvalidScanCode);
        Assert.Equal("KSP003", KeyboardProjectDiagnosticCodes.DuplicateScanCodeIdentity);
        Assert.Equal("KSP101", KeyboardProjectDiagnosticCodes.MissingProjectName);
        Assert.Equal("KSP102", KeyboardProjectDiagnosticCodes.MissingProjectVersion);
        Assert.Equal("KSP103", KeyboardProjectDiagnosticCodes.MissingProjectLanguage);
        Assert.Equal("KSP104", KeyboardProjectDiagnosticCodes.MissingProjectDescription);
        Assert.Equal("KSM001", KeyboardProjectDiagnosticCodes.MappingReferencesMissingKey);
        Assert.Equal("KSM002", KeyboardProjectDiagnosticCodes.InvalidCharacterOutput);
        Assert.Equal("KSM003", KeyboardProjectDiagnosticCodes.DuplicateKeyMapping);
        Assert.Equal("KSM100", KeyboardProjectDiagnosticCodes.OutputWithoutLogicalKey);
    }

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

        Assert.Equal(
            [
                KeyboardProjectDiagnosticCodes.MissingProjectName,
                KeyboardProjectDiagnosticCodes.MissingProjectVersion,
                KeyboardProjectDiagnosticCodes.MissingProjectLanguage
            ],
            issues.Select(issue => issue.Code));
    }

    [Fact]
    public void PhysicalKeyboardValidationRule_WhenPhysicalIdentityIsInvalid_ReportsEachProblem()
    {
        var project = DemoProjectFactory.Create();
        project.Keyboard.Keys.Add(new PhysicalKey { Id = "KeyA", ScanCode = 0x1E });
        project.Keyboard.Keys.Add(new PhysicalKey { Id = "OutOfRange", ScanCode = 0x100 });

        var issues = new PhysicalKeyboardValidationRule().Validate(project);

        Assert.Contains(issues, issue =>
            issue.Code == KeyboardProjectDiagnosticCodes.DuplicatePhysicalKeyId && issue.KeyId == "KeyA");
        Assert.Contains(issues, issue =>
            issue.Code == KeyboardProjectDiagnosticCodes.InvalidScanCode && issue.KeyId == "OutOfRange");
        Assert.Contains(issues, issue =>
            issue.Code == KeyboardProjectDiagnosticCodes.DuplicateScanCodeIdentity);
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

        Assert.Contains(issues, issue =>
            issue.Code == KeyboardProjectDiagnosticCodes.MappingReferencesMissingKey &&
            issue.KeyId == "MissingKey");
        Assert.Contains(issues, issue =>
            issue.Code == KeyboardProjectDiagnosticCodes.InvalidCharacterOutput && issue.KeyId == "KeyB");
        Assert.Contains(issues, issue =>
            issue.Code == KeyboardProjectDiagnosticCodes.DuplicateKeyMapping && issue.KeyId == "KeyA");
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
