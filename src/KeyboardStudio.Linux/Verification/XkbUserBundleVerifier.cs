using KeyboardStudio.Build;
using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>Compiles every behavior a proposed user root can affect before installation.</summary>
public sealed class XkbUserBundleVerifier : IXkbUserBundleVerifier
{
    private readonly IProcessRunner _processRunner;
    private readonly IXkbLayoutRegistryReader _registryReader;

    public XkbUserBundleVerifier()
        : this(
            new ProcessRunner(),
            new XkbRulesRegistryReader(new HostXkbFileSystem()))
    {
    }

    public XkbUserBundleVerifier(
        IProcessRunner processRunner,
        IXkbLayoutRegistryReader registryReader)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _registryReader = registryReader ?? throw new ArgumentNullException(nameof(registryReader));
    }

    public async Task<XkbUserBundleVerificationResult> VerifyAsync(
        string bundleRoot,
        IReadOnlyList<XkbUserVariantMetadata> variants,
        XkbUserInstallCapability capability,
        bool requireBundleManifest = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(capability);

        var root = Path.GetFullPath(bundleRoot);
        var diagnostics = ValidateManagedFiles(root, variants, requireBundleManifest);
        if (capability.XkbCliPath is null || capability.LibXkbCommonVersion is null)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSV002",
                "A known xkbcli/libxkbcommon toolchain is required to verify a user bundle."));
        }

        if (capability.CanonicalSystemRoot is null)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSV003",
                "The canonical system XKB root is unavailable."));
        }

        if (diagnostics.Count > 0)
        {
            return Failed(capability, [], diagnostics);
        }

        var executable = capability.XkbCliPath!;
        var checks = new List<XkbUserBundleVerificationCheck>();
        var systemRoot = new XkbDataRoot(
            capability.CanonicalSystemRoot!,
            LayoutSourceOrigin.System);
        IReadOnlyList<XkbRegistryEntry> registryEntries;
        try
        {
            registryEntries = _registryReader.Read(systemRoot);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSV004",
                $"The system XKB registry could not be read: {exception.Message}"));
            return Failed(capability, checks, diagnostics);
        }

        foreach (var metadata in variants
                     .OrderBy(metadata => metadata.BaseLayoutId, StringComparer.Ordinal)
                     .ThenBy(metadata => metadata.PublicVariantId, StringComparer.Ordinal))
        {
            checks.Add(await CompileAsync(
                executable,
                root,
                metadata.BaseLayoutId,
                metadata.PublicVariantId,
                XkbUserBundleVerificationCheckKind.CustomVariant,
                cancellationToken));

            checks.Add(await CompileAsync(
                executable,
                root,
                metadata.BaseLayoutId,
                metadata.BaseVariantId,
                XkbUserBundleVerificationCheckKind.BaseVariant,
                cancellationToken));

            var unrelated = SelectUnrelatedVariant(metadata, registryEntries);
            if (unrelated is null)
            {
                diagnostics.Add(new XkbDiagnostic(
                    "KSV004",
                    $"No unrelated system variant of layout '{metadata.BaseLayoutId}' is available for shadowing verification."));
            }
            else
            {
                checks.Add(await CompileAsync(
                    executable,
                    root,
                    metadata.BaseLayoutId,
                    unrelated,
                    XkbUserBundleVerificationCheckKind.UnrelatedVariant,
                    cancellationToken));
            }
        }

        if (capability.RegistryDiscovery == XkbRegistryDiscoverySupport.Available)
        {
            var listCheck = await VerifyRegistryAsync(
                executable,
                root,
                capability.CanonicalSystemRoot!,
                variants,
                cancellationToken);
            checks.Add(listCheck);
        }
        else
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSV006",
                "Registry discovery was not verified because libxkbregistry tooling is unavailable."));
        }

        foreach (var check in checks.Where(check => !check.Success))
        {
            diagnostics.Add(new XkbDiagnostic(
                check.Kind == XkbUserBundleVerificationCheckKind.RegistryDiscovery
                    ? "KSV006"
                    : "KSV005",
                check.Kind == XkbUserBundleVerificationCheckKind.RegistryDiscovery
                    ? "The generated variant was not discoverable in the staged registry."
                    : $"xkbcli rejected {Describe(check.LayoutId, check.VariantId)} during the {check.Kind} check."));
        }

        var failed = checks.Any(check => !check.Success) ||
            diagnostics.Any(diagnostic => diagnostic.Code is "KSV004" or "KSV005");
        var status = failed
            ? XkbUserBundleVerificationStatus.Failed
            : diagnostics.Count > 0
                ? XkbUserBundleVerificationStatus.VerifiedWithWarnings
                : XkbUserBundleVerificationStatus.Verified;

        return new XkbUserBundleVerificationResult(
            status,
            executable,
            capability.XkbCliVersionOutput,
            checks.AsReadOnly(),
            diagnostics.AsReadOnly());
    }

    public async Task<XkbUserBundleVerificationResult> VerifyBaseAsync(
        string bundleRoot,
        XkbUserVariantMetadata removedVariant,
        XkbUserInstallCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentNullException.ThrowIfNull(removedVariant);
        ArgumentNullException.ThrowIfNull(capability);

        var root = Path.GetFullPath(bundleRoot);
        var diagnostics = new List<XkbDiagnostic>();
        if (capability.XkbCliPath is null || capability.LibXkbCommonVersion is null)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSV002",
                "A known xkbcli/libxkbcommon toolchain is required to verify the base layout."));
        }

        if (capability.CanonicalSystemRoot is null)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSV003",
                "The canonical system XKB root is unavailable."));
        }

        if (diagnostics.Count > 0)
        {
            return Failed(capability, [], diagnostics);
        }

        var systemRoot = new XkbDataRoot(
            capability.CanonicalSystemRoot!,
            LayoutSourceOrigin.System);
        IReadOnlyList<XkbRegistryEntry> registryEntries;
        try
        {
            registryEntries = _registryReader.Read(systemRoot);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSV004",
                $"The system XKB registry could not be read: {exception.Message}"));
            return Failed(capability, [], diagnostics);
        }

        var checks = new List<XkbUserBundleVerificationCheck>
        {
            await CompileAsync(
                capability.XkbCliPath!,
                root,
                removedVariant.BaseLayoutId,
                removedVariant.BaseVariantId,
                XkbUserBundleVerificationCheckKind.BaseVariant,
                cancellationToken)
        };
        var unrelated = SelectUnrelatedVariant(removedVariant, registryEntries);
        if (unrelated is null)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSV004",
                $"No unrelated system variant of layout '{removedVariant.BaseLayoutId}' is available for shadowing verification."));
        }
        else
        {
            checks.Add(await CompileAsync(
                capability.XkbCliPath!,
                root,
                removedVariant.BaseLayoutId,
                unrelated,
                XkbUserBundleVerificationCheckKind.UnrelatedVariant,
                cancellationToken));
        }

        foreach (var check in checks.Where(check => !check.Success))
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSV005",
                $"xkbcli rejected {Describe(check.LayoutId, check.VariantId)} during the {check.Kind} check."));
        }

        var failed = checks.Any(check => !check.Success) ||
            diagnostics.Any(diagnostic => diagnostic.Code is "KSV004" or "KSV005");
        return new XkbUserBundleVerificationResult(
            failed
                ? XkbUserBundleVerificationStatus.Failed
                : XkbUserBundleVerificationStatus.Verified,
            capability.XkbCliPath,
            capability.XkbCliVersionOutput,
            checks.AsReadOnly(),
            diagnostics.AsReadOnly());
    }

    private async Task<XkbUserBundleVerificationCheck> CompileAsync(
        string executable,
        string root,
        string layoutId,
        string? variantId,
        XkbUserBundleVerificationCheckKind kind,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "compile-keymap",
            "--include", root,
            "--include-defaults",
            "--test",
            "--layout", layoutId
        };
        if (!string.IsNullOrEmpty(variantId))
        {
            arguments.AddRange(["--variant", variantId]);
        }

        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(
                new ProcessRequest(
                    executable,
                    arguments,
                    root,
                    new Dictionary<string, string?>()),
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new XkbUserBundleVerificationCheck(
                kind,
                layoutId,
                variantId,
                Success: false,
                arguments,
                ExitCode: null,
                StandardOutput: string.Empty,
                StandardError: exception.Message);
        }

        return new XkbUserBundleVerificationCheck(
            kind,
            layoutId,
            variantId,
            result.ExitCode == 0,
            arguments,
            result.ExitCode,
            result.StandardOutput,
            result.StandardError);
    }

    private async Task<XkbUserBundleVerificationCheck> VerifyRegistryAsync(
        string executable,
        string root,
        string systemRoot,
        IReadOnlyList<XkbUserVariantMetadata> variants,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "list",
            "--ruleset", "evdev",
            "--skip-default-paths",
            root,
            systemRoot
        };
        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(
                new ProcessRequest(
                    executable,
                    arguments,
                    root,
                    new Dictionary<string, string?>()),
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new XkbUserBundleVerificationCheck(
                XkbUserBundleVerificationCheckKind.RegistryDiscovery,
                string.Join(',', variants.Select(variant => variant.BaseLayoutId).Distinct(StringComparer.Ordinal)),
                VariantId: null,
                Success: false,
                arguments,
                ExitCode: null,
                StandardOutput: string.Empty,
                StandardError: exception.Message);
        }

        var success = result.ExitCode == 0 && variants.All(metadata =>
            RegistryContains(result.StandardOutput, metadata.BaseLayoutId, metadata.PublicVariantId));
        return new XkbUserBundleVerificationCheck(
            XkbUserBundleVerificationCheckKind.RegistryDiscovery,
            string.Join(',', variants.Select(variant => variant.BaseLayoutId).Distinct(StringComparer.Ordinal)),
            VariantId: null,
            success,
            arguments,
            result.ExitCode,
            result.StandardOutput,
            result.StandardError);
    }

    private static List<XkbDiagnostic> ValidateManagedFiles(
        string root,
        IReadOnlyList<XkbUserVariantMetadata> variants,
        bool requireBundleManifest)
    {
        var diagnostics = new List<XkbDiagnostic>();
        var required = new HashSet<string>(StringComparer.Ordinal);
        if (variants.Count > 0)
        {
            required.Add("symbols/keyboardstudio");
            required.Add("rules/evdev.xml");
        }

        if (requireBundleManifest)
        {
            required.Add("keyboardstudio-bundle.json");
        }
        foreach (var layoutId in variants.Select(variant => variant.BaseLayoutId))
        {
            required.Add($"symbols/{layoutId}");
        }

        foreach (var relative in required)
        {
            var path = Path.Combine([root, .. relative.Split('/')]);
            if (!File.Exists(path))
            {
                diagnostics.Add(new XkbDiagnostic(
                    "KSV001",
                    $"The staged bundle is missing '{relative}'."));
            }
        }

        return diagnostics;
    }

    private static string? SelectUnrelatedVariant(
        XkbUserVariantMetadata metadata,
        IReadOnlyList<XkbRegistryEntry> entries)
    {
        var variant = entries.FirstOrDefault(entry =>
            string.Equals(entry.LayoutId, metadata.BaseLayoutId, StringComparison.Ordinal) &&
            entry.VariantId is not null &&
            !string.Equals(entry.VariantId, metadata.BaseVariantId, StringComparison.Ordinal) &&
            !string.Equals(entry.VariantId, metadata.PublicVariantId, StringComparison.Ordinal));
        if (variant is not null)
        {
            return variant.VariantId;
        }

        return metadata.BaseVariantId is not null && entries.Any(entry =>
            string.Equals(entry.LayoutId, metadata.BaseLayoutId, StringComparison.Ordinal) &&
            entry.VariantId is null)
            ? string.Empty
            : null;
    }

    private static bool RegistryContains(string yaml, string layoutId, string variantId)
    {
        string? currentLayout = null;
        foreach (var rawLine in yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim().TrimStart('-').TrimStart();
            if (line.StartsWith("layout:", StringComparison.Ordinal))
            {
                currentLayout = YamlValue(line["layout:".Length..]);
            }
            else if (line.StartsWith("variant:", StringComparison.Ordinal) &&
                     string.Equals(currentLayout, layoutId, StringComparison.Ordinal) &&
                     string.Equals(YamlValue(line["variant:".Length..]), variantId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string YamlValue(string value) =>
        value.Trim().Trim('\'', '"');

    private static string Describe(string layout, string? variant) =>
        variant is null ? layout : $"{layout}({variant})";

    private static XkbUserBundleVerificationResult Failed(
        XkbUserInstallCapability capability,
        IReadOnlyList<XkbUserBundleVerificationCheck> checks,
        IReadOnlyList<XkbDiagnostic> diagnostics) =>
        new(
            XkbUserBundleVerificationStatus.Failed,
            capability.XkbCliPath,
            capability.XkbCliVersionOutput,
            checks,
            diagnostics);
}
