using KeyboardStudio.Core;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// A catalog with a handful of entries and no platform behind it.
///
/// It records what it was asked for, which is how the tests check that a choice made in the dialog
/// reached the source as an option rather than being quietly dropped on the way.
/// </summary>
internal sealed class FakeLayoutImportCatalog : ILayoutImportCatalog
{
    /// <summary>
    /// The geometry the fake infers when the caller names none. Deliberately not the application's
    /// default, so a test can tell a suggestion that was taken from one that was assumed.
    /// </summary>
    public const string SuggestedTemplateId = "ansi-104";

    private readonly List<ImportableLayoutDescriptor> _descriptors = [];

    public bool HasAvailableSources { get; init; } = true;

    public bool FailImport { get; init; }

    public ImportableLayoutReference? LastReference { get; private set; }

    public LayoutImportOptions? LastOptions { get; private set; }

    public int ImportCount { get; private set; }

    public IReadOnlyList<ImportableLayoutDescriptor> Descriptors => _descriptors;

    public FakeLayoutImportCatalog Add(
        string layoutId,
        string? variantId,
        string displayName,
        IReadOnlyList<string>? languages = null,
        IReadOnlyList<string>? countries = null)
    {
        _descriptors.Add(new ImportableLayoutDescriptor(
            "fake",
            layoutId,
            variantId,
            displayName,
            ShortDescription: null,
            languages ?? [],
            countries ?? [],
            LayoutSourceOrigin.System,
            $"/xkb/symbols/{layoutId}"));
        return this;
    }

    public Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ImportableLayoutDescriptor>>(_descriptors);

    public Task<LayoutImportResult> ImportAsync(
        ImportableLayoutReference reference,
        LayoutImportOptions options,
        CancellationToken cancellationToken = default)
    {
        LastReference = reference;
        LastOptions = options;
        ImportCount++;

        var report = new LayoutImportReport(
            LayoutImportFidelity.Exact,
            KeysImported: 1,
            KeysSkipped: 0,
            [reference.LayoutId],
            []);

        if (FailImport)
        {
            return Task.FromResult(LayoutImportResult.Failed(report with
            {
                Fidelity = LayoutImportFidelity.Partial,
                KeysImported = 0,
                Diagnostics =
                [
                    new LayoutImportDiagnostic(
                        ValidationSeverity.Error,
                        LayoutImportDiagnosticCodes.CompositionTargetUnavailable,
                        "Nothing defines this layout.")
                ]
            }));
        }

        var templateId = options.TemplateId ?? SuggestedTemplateId;
        var keyboard = new KeyboardTemplateProvider().Load(templateId);

        var project = new KeyboardProject
        {
            Metadata = new ProjectMetadata
            {
                Name = options.ProjectName ?? reference.LayoutId,
                Description = $"Imported from {reference.LayoutId}."
            },
            Keyboard = keyboard,
            Layout = new KeyboardLayout
            {
                Mappings =
                {
                    new KeyMapping
                    {
                        KeyId = "KeyA",
                        LogicalKey = LogicalKey.A,
                        Outputs = { [ModifierLayer.Default] = new CharacterOutput("ä") }
                    }
                }
            }
        };

        return Task.FromResult(LayoutImportResult.Succeeded(
            project,
            templateId,
            report,
            reference.VariantId ?? "basic"));
    }
}
