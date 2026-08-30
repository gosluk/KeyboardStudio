using System.Security.Cryptography;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App;

/// <summary>Composes Linux generation, capability, ownership, and installation services for the UI.</summary>
public sealed class LinuxUserVariantWorkflowService : ILinuxUserVariantWorkflowService
{
    private readonly IXkbUserInstallCapabilityProbe _capabilityProbe;
    private readonly XdgDirectoryResolver _directoryResolver;
    private readonly XkbUserVariantTranslator _translator;
    private readonly IXkbUserBundleWriter _bundleWriter;
    private readonly IXkbUserInstallService _installService;

    public LinuxUserVariantWorkflowService()
        : this(CreateDefaultDependencies())
    {
    }

    private LinuxUserVariantWorkflowService(DefaultDependencies dependencies)
        : this(
            dependencies.CapabilityProbe,
            dependencies.DirectoryResolver,
            new XkbUserVariantTranslator(),
            new XkbUserBundleWriter(),
            new XkbUserInstallService())
    {
    }

    public LinuxUserVariantWorkflowService(
        IXkbUserInstallCapabilityProbe capabilityProbe,
        XdgDirectoryResolver directoryResolver,
        XkbUserVariantTranslator translator,
        IXkbUserBundleWriter bundleWriter,
        IXkbUserInstallService installService)
    {
        _capabilityProbe = capabilityProbe ?? throw new ArgumentNullException(nameof(capabilityProbe));
        _directoryResolver = directoryResolver ?? throw new ArgumentNullException(nameof(directoryResolver));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _bundleWriter = bundleWriter ?? throw new ArgumentNullException(nameof(bundleWriter));
        _installService = installService ?? throw new ArgumentNullException(nameof(installService));
    }

    public async Task<LinuxUserVariantPreparation> InspectAsync(
        KeyboardProject project,
        LayoutDerivation? derivation,
        string? publicVariantId,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (derivation is null)
        {
            return Unavailable(
                LinuxUserVariantStatus.Unavailable,
                "KSW001",
                "Import a layout from the installed system catalog as a new project before creating a user variant.");
        }

        var pathResolution = _directoryResolver.Resolve();
        var capability = await _capabilityProbe.ProbeAsync(cancellationToken);
        var diagnostics = pathResolution.Diagnostics.Concat(capability.Diagnostics).ToList();
        var paths = pathResolution.Paths;
        XkbInstallationManifest? manifest = null;
        XkbInstalledVariant? installed = null;
        if (paths is not null)
        {
            try
            {
                var manifestPath = Path.Combine(paths.KeyboardStudioStateRoot, "installations.json");
                if (IsSymbolicLink(paths.KeyboardStudioStateRoot) || IsSymbolicLink(manifestPath))
                {
                    throw new InvalidDataException("The host-local state path is a symbolic link.");
                }

                manifest = File.Exists(manifestPath)
                    ? XkbInstallationManifestSerializer.Deserialize(
                        await File.ReadAllTextAsync(manifestPath, cancellationToken))
                    : XkbInstallationManifest.Empty;
                installed = manifest.Installations.SingleOrDefault(item => string.Equals(
                    item.ProjectInstallationId,
                    derivation.ProjectInstallationId,
                    StringComparison.Ordinal));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                diagnostics.Add(new XkbDiagnostic(
                    "KSW004",
                    $"The host-local installation state is unreadable: {exception.Message}"));
                return new LinuxUserVariantPreparation(
                    LinuxUserVariantStatus.Broken,
                    null,
                    null,
                    paths,
                    capability,
                    null,
                    diagnostics);
            }
        }

        var requestedId = string.IsNullOrWhiteSpace(publicVariantId)
            ? installed?.PublicVariantId ?? DefaultVariantId(derivation)
            : publicVariantId.Trim();
        var requestedName = string.IsNullOrWhiteSpace(displayName)
            ? installed?.Description ?? $"{project.Metadata.Name} - KeyboardStudio"
            : displayName.Trim();
        if (!IsValidIdentifier(requestedId))
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSW002",
                "The custom variant ID must start with an ASCII letter and use only lowercase letters, digits, '_' or '-'."));
            return new LinuxUserVariantPreparation(
                LinuxUserVariantStatus.Unavailable,
                null,
                null,
                paths,
                capability,
                manifest,
                diagnostics);
        }

        if (string.IsNullOrWhiteSpace(requestedName))
        {
            diagnostics.Add(new XkbDiagnostic("KSW003", "A display name is required."));
            return new LinuxUserVariantPreparation(
                LinuxUserVariantStatus.Unavailable,
                null,
                null,
                paths,
                capability,
                manifest,
                diagnostics);
        }

        var metadata = new XkbUserVariantMetadata(
            derivation.ProjectInstallationId,
            derivation.BaseLayoutId,
            derivation.BaseVariantId,
            derivation.ResolvedBaseSectionId,
            requestedId,
            requestedName);
        var translation = _translator.Translate(project, derivation.BaselineMappings, metadata);
        diagnostics.AddRange(translation.Diagnostics);
        if (!translation.Success)
        {
            return new LinuxUserVariantPreparation(
                LinuxUserVariantStatus.Unavailable,
                metadata,
                null,
                paths,
                capability,
                manifest,
                diagnostics);
        }

        var generated = XkbUserBundleGenerator.Generate([translation.Layout!]);
        diagnostics.AddRange(generated.Diagnostics);
        if (!generated.Success)
        {
            return new LinuxUserVariantPreparation(
                LinuxUserVariantStatus.Unavailable,
                metadata,
                null,
                paths,
                capability,
                manifest,
                diagnostics);
        }

        var status = DetermineStatus(
            derivation,
            metadata,
            generated.Bundle!,
            paths,
            capability,
            manifest,
            installed,
            diagnostics);
        return new LinuxUserVariantPreparation(
            status,
            metadata,
            generated.Bundle,
            paths,
            capability,
            manifest,
            diagnostics);
    }

    public async Task<LinuxUserVariantOperationResult> GenerateAsync(
        LinuxUserVariantPreparation preparation,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (preparation.Bundle is null)
        {
            return Failure("The user-variant bundle is not ready to generate.", preparation.Diagnostics);
        }

        try
        {
            var write = await _bundleWriter.WriteAsync(
                preparation.Bundle,
                outputDirectory,
                cancellationToken);
            return new LinuxUserVariantOperationResult(
                true,
                $"Generated the user XKB bundle at {write.BundleRoot}.",
                write.BundleRoot,
                []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return Failure($"Bundle generation failed: {exception.Message}");
        }
    }

    public async Task<LinuxUserVariantOperationResult> InstallOrUpdateAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetManagedInputs(preparation, out var bundle, out var metadata, out var paths, out var capability))
        {
            return Failure("Managed installation is unavailable.", preparation.Diagnostics);
        }

        var result = await _installService.InstallOrUpdateAsync(
            bundle!, metadata!, paths!, capability!, cancellationToken);
        return FromInstallResult(
            result,
            result.Command == XkbUserInstallCommand.Install
                ? "Installed the user XKB variant. Reopen desktop keyboard settings or restart the session if it is not listed yet."
                : "Updated the user XKB variant. Reopen desktop keyboard settings or restart the session if needed.");
    }

    public async Task<LinuxUserVariantOperationResult> VerifyInstalledAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetManagedInputs(preparation, out _, out var metadata, out var paths, out var capability))
        {
            return Failure("Installed verification is unavailable.", preparation.Diagnostics);
        }

        var result = await _installService.VerifyInstalledAsync(
            metadata!.ProjectInstallationId, paths!, capability!, cancellationToken);
        return FromInstallResult(result, "The installed custom, base, and unrelated variants verified successfully.");
    }

    public async Task<LinuxUserVariantOperationResult> UninstallAsync(
        LinuxUserVariantPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetManagedInputs(preparation, out _, out var metadata, out var paths, out var capability))
        {
            return Failure("Managed uninstall is unavailable.", preparation.Diagnostics);
        }

        var result = await _installService.UninstallAsync(
            metadata!.ProjectInstallationId, paths!, capability!, cancellationToken);
        return FromInstallResult(
            result,
            "Uninstalled the KeyboardStudio variant without changing the system layout or active desktop layout.");
    }

    private static LinuxUserVariantStatus DetermineStatus(
        LayoutDerivation derivation,
        XkbUserVariantMetadata metadata,
        XkbGeneratedUserBundle bundle,
        XdgDirectoryPaths? paths,
        XkbUserInstallCapability capability,
        XkbInstallationManifest? manifest,
        XkbInstalledVariant? installed,
        List<XkbDiagnostic> diagnostics)
    {
        if (capability.CanonicalSystemRoot is null)
        {
            return LinuxUserVariantStatus.BaseUnavailable;
        }

        if (paths is null || manifest is null)
        {
            return LinuxUserVariantStatus.Unavailable;
        }

        if (installed is null)
        {
            var collision = manifest.Installations.Any(item =>
                string.Equals(item.BaseLayoutId, metadata.BaseLayoutId, StringComparison.Ordinal) &&
                string.Equals(item.PublicVariantId, metadata.PublicVariantId, StringComparison.Ordinal));
            if (collision)
            {
                diagnostics.Add(new XkbDiagnostic(
                    "KSW005",
                    $"Another KeyboardStudio project already owns '{metadata.BaseLayoutId}({metadata.PublicVariantId})'."));
                return LinuxUserVariantStatus.Unavailable;
            }

            return capability.Mode == XkbUserInstallMode.ManagedInstallation
                ? LinuxUserVariantStatus.NotInstalled
                : LinuxUserVariantStatus.ExportOnly;
        }

        var ownership = InspectOwnership(paths, manifest, installed);
        diagnostics.AddRange(ownership.Diagnostics);
        if (ownership.Missing)
        {
            return LinuxUserVariantStatus.Broken;
        }

        if (!ownership.Valid)
        {
            return LinuxUserVariantStatus.ExternallyModified;
        }

        var stagedCentral = XkbManagedBlockEditor.Read(
            bundle.Find("symbols/keyboardstudio")!.Content,
            derivation.ProjectInstallationId);
        var stagedBridge = XkbManagedBlockEditor.Read(
            bundle.Find($"symbols/{derivation.BaseLayoutId}")!.Content,
            derivation.ProjectInstallationId);
        var stagedRegistry = XkbRegistryDocumentMerger.Upsert(null, metadata, null);
        var changed = !string.Equals(installed.PublicVariantId, metadata.PublicVariantId, StringComparison.Ordinal) ||
                      !string.Equals(installed.Description, metadata.Description, StringComparison.Ordinal) ||
                      !string.Equals(installed.CentralBlockSha256, stagedCentral.ManagedBlockSha256, StringComparison.Ordinal) ||
                      !string.Equals(installed.BridgeBlockSha256, stagedBridge.ManagedBlockSha256, StringComparison.Ordinal) ||
                      !string.Equals(installed.RegistryEntrySha256, stagedRegistry.EntrySha256, StringComparison.Ordinal);
        return changed
            ? LinuxUserVariantStatus.UpdateAvailable
            : LinuxUserVariantStatus.Installed;
    }

    private static OwnershipInspection InspectOwnership(
        XdgDirectoryPaths paths,
        XkbInstallationManifest manifest,
        XkbInstalledVariant installed)
    {
        var centralPath = Path.Combine(paths.UserXkbRoot, "symbols", "keyboardstudio");
        var bridgePath = Path.Combine(paths.UserXkbRoot, "symbols", installed.BaseLayoutId);
        var registryPath = Path.Combine(paths.UserXkbRoot, "rules", "evdev.xml");
        if (IsSymbolicLink(paths.UserXkbRoot) ||
            IsSymbolicLink(centralPath) ||
            IsSymbolicLink(bridgePath) ||
            IsSymbolicLink(registryPath))
        {
            return new OwnershipInspection(
                false,
                false,
                [new XkbDiagnostic("KSW007", "An installed XKB path is a symbolic link and will not be inspected.")]);
        }

        if (!File.Exists(centralPath) || !File.Exists(bridgePath) || !File.Exists(registryPath))
        {
            return new OwnershipInspection(
                false,
                true,
                [new XkbDiagnostic("KSW006", "One or more files recorded by the installation manifest are missing.")]);
        }

        try
        {
            var centralRecord = manifest.Files.SingleOrDefault(file =>
                string.Equals(file.RelativePath, "symbols/keyboardstudio", StringComparison.Ordinal));
            var central = File.ReadAllText(centralPath);
            var bridge = File.ReadAllText(bridgePath);
            var registry = File.ReadAllText(registryPath);
            var centralBlock = XkbManagedBlockEditor.Read(central, installed.ProjectInstallationId);
            var bridgeBlock = XkbManagedBlockEditor.Read(bridge, installed.ProjectInstallationId);
            var registryEntry = XkbRegistryDocumentMerger.Upsert(
                registry,
                ToMetadata(installed),
                installed.RegistryEntrySha256);
            var valid = centralRecord is not null &&
                        string.Equals(HashFile(centralPath), centralRecord.Sha256, StringComparison.Ordinal) &&
                        centralBlock.Success &&
                        string.Equals(centralBlock.ManagedBlockSha256, installed.CentralBlockSha256, StringComparison.Ordinal) &&
                        bridgeBlock.Success &&
                        string.Equals(bridgeBlock.ManagedBlockSha256, installed.BridgeBlockSha256, StringComparison.Ordinal) &&
                        registryEntry.Success;
            return valid
                ? new OwnershipInspection(true, false, [])
                : new OwnershipInspection(
                    false,
                    false,
                    [new XkbDiagnostic("KSW007", "Installed KeyboardStudio-owned content was modified outside the application.")]);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return new OwnershipInspection(
                false,
                false,
                [new XkbDiagnostic("KSW007", $"Installed ownership could not be verified: {exception.Message}")]);
        }
    }

    private static bool TryGetManagedInputs(
        LinuxUserVariantPreparation preparation,
        out XkbGeneratedUserBundle? bundle,
        out XkbUserVariantMetadata? metadata,
        out XdgDirectoryPaths? paths,
        out XkbUserInstallCapability? capability)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        bundle = preparation.Bundle;
        metadata = preparation.Metadata;
        paths = preparation.Paths;
        capability = preparation.Capability;
        return bundle is not null && metadata is not null && paths is not null &&
               capability?.Mode == XkbUserInstallMode.ManagedInstallation;
    }

    private static LinuxUserVariantOperationResult FromInstallResult(
        XkbUserInstallResult result,
        string successMessage) =>
        new(
            result.Success,
            result.Success
                ? successMessage
                : result.Diagnostics.Count > 0
                    ? result.Diagnostics[0].Message
                    : "The live XKB operation failed.",
            null,
            result.Diagnostics);

    private static LinuxUserVariantOperationResult Failure(
        string message,
        IReadOnlyList<XkbDiagnostic>? diagnostics = null) =>
        new(false, message, null, diagnostics ?? []);

    private static LinuxUserVariantPreparation Unavailable(
        LinuxUserVariantStatus status,
        string code,
        string message) =>
        new(status, null, null, null, null, null, [new XkbDiagnostic(code, message)]);

    private static string DefaultVariantId(LayoutDerivation derivation) =>
        XkbLayoutMetadata.SanitizeIdentifier(
            $"keyboardstudio_{derivation.BaseVariantId ?? "custom"}",
            "keyboardstudio_custom");

    private static bool IsValidIdentifier(string value) =>
        value.Length is > 0 and <= 64 &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');

    private static XkbUserVariantMetadata ToMetadata(XkbInstalledVariant installed) =>
        new(
            installed.ProjectInstallationId,
            installed.BaseLayoutId,
            installed.BaseVariantId,
            installed.ResolvedBaseSectionId,
            installed.PublicVariantId,
            installed.Description);

    private static string HashFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static bool IsSymbolicLink(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static DefaultDependencies CreateDefaultDependencies()
    {
        var environment = new HostXkbEnvironment();
        var fileSystem = new HostXkbFileSystem();
        var roots = new XkbDataRootLocator(environment, fileSystem);
        return new DefaultDependencies(
            new XkbUserInstallCapabilityProbe(
                environment,
                roots,
                new PathXkbCliLocator(),
                new ProcessRunner()),
            new XdgDirectoryResolver(environment));
    }

    private sealed record DefaultDependencies(
        IXkbUserInstallCapabilityProbe CapabilityProbe,
        XdgDirectoryResolver DirectoryResolver);

    private sealed record OwnershipInspection(
        bool Valid,
        bool Missing,
        IReadOnlyList<XkbDiagnostic> Diagnostics);
}
