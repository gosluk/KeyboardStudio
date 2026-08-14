namespace KeyboardStudio.Core;

public sealed class KeyboardProjectValidator : IKeyboardProjectValidator
{
    private readonly IReadOnlyList<IKeyboardProjectValidationRule> _rules;

    public KeyboardProjectValidator()
        : this([
            new MetadataValidationRule(),
            new PhysicalKeyboardValidationRule(),
            new MappingValidationRule()
        ])
    {
    }

    public KeyboardProjectValidator(IEnumerable<IKeyboardProjectValidationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToArray();
        if (_rules.Any(rule => rule is null))
        {
            throw new ArgumentException("Validation rules must not contain null entries.", nameof(rules));
        }
    }

    public IReadOnlyList<ValidationIssue> Validate(KeyboardProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return _rules.SelectMany(rule => rule.Validate(project)).ToArray();
    }
}
