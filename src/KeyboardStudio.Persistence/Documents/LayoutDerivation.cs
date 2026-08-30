using KeyboardStudio.Core;

namespace KeyboardStudio.Persistence;

/// <summary>
/// The immutable system-layout state from which a project was derived.
/// </summary>
public sealed class LayoutDerivation
{
    public LayoutDerivation(
        string projectInstallationId,
        string sourceId,
        LayoutSourceOrigin sourceOrigin,
        string baseLayoutId,
        string? baseVariantId,
        string resolvedBaseSectionId,
        DateTimeOffset importedAtUtc,
        LayoutImportFidelity importFidelity,
        IReadOnlyList<KeyMappingSnapshot> baselineMappings,
        string? sourceFingerprint = null,
        string? includeChainFingerprint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectInstallationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseLayoutId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedBaseSectionId);
        ArgumentNullException.ThrowIfNull(baselineMappings);

        if (sourceOrigin != LayoutSourceOrigin.System)
        {
            throw new ArgumentException(
                "An installable layout derivation must originate from the system catalog.",
                nameof(sourceOrigin));
        }

        ProjectInstallationId = projectInstallationId;
        SourceId = sourceId;
        SourceOrigin = sourceOrigin;
        BaseLayoutId = baseLayoutId;
        BaseVariantId = baseVariantId;
        ResolvedBaseSectionId = resolvedBaseSectionId;
        ImportedAtUtc = importedAtUtc;
        ImportFidelity = importFidelity;
        BaselineMappings = Array.AsReadOnly(baselineMappings.ToArray());
        SourceFingerprint = sourceFingerprint;
        IncludeChainFingerprint = includeChainFingerprint;
    }

    public string ProjectInstallationId { get; }

    public string SourceId { get; }

    public LayoutSourceOrigin SourceOrigin { get; }

    public string BaseLayoutId { get; }

    public string? BaseVariantId { get; }

    public string ResolvedBaseSectionId { get; }

    public DateTimeOffset ImportedAtUtc { get; }

    public LayoutImportFidelity ImportFidelity { get; }

    public IReadOnlyList<KeyMappingSnapshot> BaselineMappings { get; }

    public string? SourceFingerprint { get; }

    public string? IncludeChainFingerprint { get; }
}
