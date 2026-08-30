using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbUserInstallServiceTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task InstallOrUpdateAsync_AbsentRoots_VerifiesBeforeAndAfterThenCommitsManifest()
    {
        using var scope = new TemporaryXdgScope();
        var verifier = new RecordingVerifier();
        var service = new XkbUserInstallService(verifier);
        var metadata = Polish();

        var result = await service.InstallOrUpdateAsync(
            Bundle(metadata, "x"), metadata, scope.Paths, Capability(scope.Paths));

        Assert.True(result.Success);
        Assert.Equal(XkbUserInstallCommand.Install, result.Command);
        Assert.Equal(2, verifier.VariantRoots.Count);
        Assert.NotEqual(scope.Paths.UserXkbRoot, verifier.VariantRoots[0]);
        Assert.Equal(scope.Paths.UserXkbRoot, verifier.VariantRoots[1]);
        Assert.True(File.Exists(Path.Combine(scope.Paths.UserXkbRoot, "symbols", "keyboardstudio")));
        Assert.True(File.Exists(Path.Combine(scope.Paths.UserXkbRoot, "symbols", "pl")));
        Assert.True(File.Exists(Path.Combine(scope.Paths.UserXkbRoot, "rules", "evdev.xml")));
        Assert.True(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "installations.json")));
        Assert.False(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "journal.json")));
        Assert.False(Directory.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "backups")));
        Assert.Single(result.Manifest!.Installations);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateAndUninstall_PreserveUnrelatedSharedContentAndRemoveOnlyOwnedFiles()
    {
        using var scope = new TemporaryXdgScope();
        var service = new XkbUserInstallService(new RecordingVerifier());
        var metadata = Polish();
        var install = await service.InstallOrUpdateAsync(
            Bundle(metadata, "x"), metadata, scope.Paths, Capability(scope.Paths));
        Assert.True(install.Success);

        var bridgePath = Path.Combine(scope.Paths.UserXkbRoot, "symbols", "pl");
        var registryPath = Path.Combine(scope.Paths.UserXkbRoot, "rules", "evdev.xml");
        await File.AppendAllTextAsync(bridgePath, "\n// user section\nxkb_symbols \"mine\" { };\n");
        var registry = await File.ReadAllTextAsync(registryPath);
        await File.WriteAllTextAsync(
            registryPath,
            registry.Replace(
                "</xkbConfigRegistry>",
                "<unknown keep=\"yes\" /></xkbConfigRegistry>",
                StringComparison.Ordinal));

        var update = await service.InstallOrUpdateAsync(
            Bundle(metadata, "y"), metadata, scope.Paths, Capability(scope.Paths));
        Assert.True(update.Success);
        Assert.Equal(XkbUserInstallCommand.Update, update.Command);
        Assert.Contains("symbols[Group1] = [ y, Y ]", await File.ReadAllTextAsync(
            Path.Combine(scope.Paths.UserXkbRoot, "symbols", "keyboardstudio")));

        var uninstall = await service.UninstallAsync(
            metadata.ProjectInstallationId, scope.Paths, Capability(scope.Paths));

        Assert.True(uninstall.Success);
        Assert.Empty(uninstall.Manifest!.Installations);
        Assert.False(File.Exists(Path.Combine(scope.Paths.UserXkbRoot, "symbols", "keyboardstudio")));
        Assert.Contains("xkb_symbols \"mine\"", await File.ReadAllTextAsync(bridgePath));
        Assert.Contains("<unknown keep=\"yes\"", await File.ReadAllTextAsync(registryPath));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ErrorPath")]
    public async Task InstallOrUpdateAsync_WhenProposedVerificationFails_DoesNotTouchLiveRoot()
    {
        using var scope = new TemporaryXdgScope();
        var verifier = new RecordingVerifier { FailVariantCall = 1 };
        var metadata = Polish();

        var result = await new XkbUserInstallService(verifier).InstallOrUpdateAsync(
            Bundle(metadata, "x"), metadata, scope.Paths, Capability(scope.Paths));

        Assert.False(result.Success);
        Assert.False(result.RolledBack);
        Assert.False(Directory.Exists(scope.Paths.UserXkbRoot));
        Assert.False(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "installations.json")));
        Assert.False(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "journal.json")));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ErrorPath")]
    public async Task InstallOrUpdateAsync_WhenInstalledVerificationFails_RollsBackAllDestinations()
    {
        using var scope = new TemporaryXdgScope();
        var verifier = new RecordingVerifier { FailVariantCall = 2 };
        var metadata = Polish();

        var result = await new XkbUserInstallService(verifier).InstallOrUpdateAsync(
            Bundle(metadata, "x"), metadata, scope.Paths, Capability(scope.Paths));

        Assert.False(result.Success);
        Assert.True(result.RolledBack);
        Assert.False(Directory.Exists(scope.Paths.UserXkbRoot) &&
                     Directory.EnumerateFiles(scope.Paths.UserXkbRoot, "*", SearchOption.AllDirectories).Any());
        Assert.False(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "installations.json")));
        Assert.False(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "journal.json")));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [Trait("Category", "ErrorPath")]
    [InlineData(XkbInstallTransactionStep.ProposedRootPrepared, 1)]
    [InlineData(XkbInstallTransactionStep.ProposedRootVerified, 1)]
    [InlineData(XkbInstallTransactionStep.BackupsPrepared, 1)]
    [InlineData(XkbInstallTransactionStep.JournalWritten, 1)]
    [InlineData(XkbInstallTransactionStep.DestinationApplied, 1)]
    [InlineData(XkbInstallTransactionStep.DestinationApplied, 2)]
    [InlineData(XkbInstallTransactionStep.DestinationApplied, 3)]
    [InlineData(XkbInstallTransactionStep.InstalledRootVerified, 1)]
    [InlineData(XkbInstallTransactionStep.ManifestWritten, 1)]
    public async Task InstallOrUpdateAsync_FailureAfterEachMutationMilestone_RestoresPreTransactionState(
        XkbInstallTransactionStep milestone,
        int occurrence)
    {
        using var scope = new TemporaryXdgScope();
        var observer = new ThrowingObserver(milestone, occurrence, crash: false);
        var service = new XkbUserInstallService(new RecordingVerifier(), observer: observer);
        var metadata = Polish();

        var result = await service.InstallOrUpdateAsync(
            Bundle(metadata, "x"), metadata, scope.Paths, Capability(scope.Paths));

        Assert.False(result.Success);
        Assert.False(Directory.Exists(scope.Paths.UserXkbRoot) &&
                     Directory.EnumerateFiles(scope.Paths.UserXkbRoot, "*", SearchOption.AllDirectories).Any());
        Assert.False(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "installations.json")));
        Assert.False(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "journal.json")));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ErrorPath")]
    public async Task NextOperation_RecoversJournalLeftByInterruptedProcessBeforeInstalling()
    {
        using var scope = new TemporaryXdgScope();
        var metadata = Polish();
        var crashing = new XkbUserInstallService(
            new RecordingVerifier(),
            observer: new ThrowingObserver(
                XkbInstallTransactionStep.DestinationApplied,
                occurrence: 1,
                crash: true));

        await Assert.ThrowsAsync<SimulatedCrashException>(() => crashing.InstallOrUpdateAsync(
            Bundle(metadata, "x"), metadata, scope.Paths, Capability(scope.Paths)));
        Assert.True(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "journal.json")));

        var result = await new XkbUserInstallService(new RecordingVerifier()).InstallOrUpdateAsync(
            Bundle(metadata, "x"), metadata, scope.Paths, Capability(scope.Paths));

        Assert.True(result.Success);
        Assert.True(result.RecoveredInterruptedTransaction);
        Assert.False(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "journal.json")));
        Assert.Single(result.Manifest!.Installations);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ErrorPath")]
    public async Task InstallOrUpdateAsync_CancellationAfterMutation_RollsBackBeforeRethrowing()
    {
        using var scope = new TemporaryXdgScope();
        using var cancellation = new CancellationTokenSource();
        var observer = new CancellingObserver(cancellation);
        var service = new XkbUserInstallService(new RecordingVerifier(), observer: observer);
        var metadata = Polish();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InstallOrUpdateAsync(
            Bundle(metadata, "x"),
            metadata,
            scope.Paths,
            Capability(scope.Paths),
            cancellation.Token));

        Assert.False(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "journal.json")));
        Assert.False(Directory.Exists(scope.Paths.UserXkbRoot) &&
                     Directory.EnumerateFiles(scope.Paths.UserXkbRoot, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ErrorPath")]
    public async Task VerifyInstalledAsync_WhenOwnedBlockChanged_ReportsConflictBeforeTooling()
    {
        using var scope = new TemporaryXdgScope();
        var verifier = new RecordingVerifier();
        var service = new XkbUserInstallService(verifier);
        var metadata = Polish();
        Assert.True((await service.InstallOrUpdateAsync(
            Bundle(metadata, "x"), metadata, scope.Paths, Capability(scope.Paths))).Success);
        var bridgePath = Path.Combine(scope.Paths.UserXkbRoot, "symbols", "pl");
        var bridge = await File.ReadAllTextAsync(bridgePath);
        await File.WriteAllTextAsync(
            bridgePath,
            bridge.Replace("include", "// changed\n    include", StringComparison.Ordinal));
        var callsBefore = verifier.VariantRoots.Count;

        var result = await service.VerifyInstalledAsync(
            metadata.ProjectInstallationId, scope.Paths, Capability(scope.Paths));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSI006");
        Assert.Equal(callsBefore, verifier.VariantRoots.Count);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ErrorPath")]
    public async Task InstallOrUpdateAsync_WhenUserRootContainsSymlink_RefusesToTraverseIt()
    {
        using var scope = new TemporaryXdgScope();
        var symbols = Path.Combine(scope.Paths.UserXkbRoot, "symbols");
        var outside = Path.Combine(scope.Root, "outside");
        Directory.CreateDirectory(symbols);
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "sentinel"), "untouched");
        Directory.CreateSymbolicLink(Path.Combine(symbols, "linked"), outside);
        var metadata = Polish();

        var result = await new XkbUserInstallService(new RecordingVerifier()).InstallOrUpdateAsync(
            Bundle(metadata, "x"), metadata, scope.Paths, Capability(scope.Paths));

        Assert.False(result.Success);
        Assert.Equal("untouched", await File.ReadAllTextAsync(Path.Combine(outside, "sentinel")));
        Assert.False(File.Exists(Path.Combine(scope.Paths.KeyboardStudioStateRoot, "journal.json")));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ErrorPath")]
    public async Task InstallOrUpdateAsync_WhenManagedRootIsGroupWritable_RefusesIt()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = new TemporaryXdgScope();
        Directory.CreateDirectory(scope.Paths.UserXkbRoot);
        File.SetUnixFileMode(
            scope.Paths.UserXkbRoot,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute);
        var metadata = Polish();

        var result = await new XkbUserInstallService(new RecordingVerifier()).InstallOrUpdateAsync(
            Bundle(metadata, "x"), metadata, scope.Paths, Capability(scope.Paths));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSI004");
        Assert.Empty(Directory.EnumerateFileSystemEntries(scope.Paths.UserXkbRoot));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task InstallOrUpdateAsync_InExportOnlyMode_RefusesBeforeCreatingDirectories()
    {
        using var scope = new TemporaryXdgScope();
        var metadata = Polish();
        var capability = Capability(scope.Paths) with { Mode = XkbUserInstallMode.ExportOnly };

        var result = await new XkbUserInstallService(new RecordingVerifier()).InstallOrUpdateAsync(
            Bundle(metadata, "x"), metadata, scope.Paths, capability);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSI001");
        Assert.False(Directory.Exists(scope.Root));
    }

    private static XkbGeneratedUserBundle Bundle(XkbUserVariantMetadata metadata, string symbol) =>
        XkbUserBundleGenerator.Generate(
        [
            new XkbUserVariantLayout(
                metadata,
                [new XkbUserVariantKeyMapping(
                    "KeyA",
                    "<AC01>",
                    XkbKeyType.Alphabetic,
                    [symbol, symbol.ToUpperInvariant()])],
                UsesLevelThree: false)
        ]).Bundle!;

    private static XkbUserVariantMetadata Polish() => new(
        "7c31d5f2a19e40a4b0ef64f01a295135",
        "pl",
        "qwertz",
        "qwertz",
        "keyboardstudio_programmer",
        "Polish - KeyboardStudio");

    private static XkbUserInstallCapability Capability(XdgDirectoryPaths paths) => new(
        XkbUserInstallMode.ManagedInstallation,
        XkbSessionType.Wayland,
        paths.UserXkbRoot,
        paths.KeyboardStudioStateRoot,
        PathsAreSafe: true,
        "/usr/bin/xkbcli",
        "xkbcli 1.13.1",
        new Version(1, 13, 1),
        MeetsRecommendedVersion: true,
        "/usr/share/X11/xkb",
        XkbRegistryDiscoverySupport.Available,
        []);

    private sealed class TemporaryXdgScope : IDisposable
    {
        public TemporaryXdgScope()
        {
            Root = Path.Combine(Path.GetTempPath(), $"keyboardstudio-install-{Guid.NewGuid():N}");
            Paths = new XdgDirectoryPaths(
                Path.Combine(Root, "config"),
                Path.Combine(Root, "state"),
                Path.Combine(Root, "config", "xkb"),
                Path.Combine(Root, "state", "keyboardstudio", "xkb"));
        }

        public string Root { get; }

        public XdgDirectoryPaths Paths { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class RecordingVerifier : IXkbUserBundleVerifier
    {
        public List<string> VariantRoots { get; } = [];

        public int? FailVariantCall { get; init; }

        public Task<XkbUserBundleVerificationResult> VerifyAsync(
            string bundleRoot,
            IReadOnlyList<XkbUserVariantMetadata> variants,
            XkbUserInstallCapability capability,
            bool requireBundleManifest = true,
            CancellationToken cancellationToken = default)
        {
            VariantRoots.Add(bundleRoot);
            cancellationToken.ThrowIfCancellationRequested();
            Assert.All(variants, variant =>
            {
                Assert.True(File.Exists(Path.Combine(bundleRoot, "symbols", "keyboardstudio")));
                Assert.True(File.Exists(Path.Combine(bundleRoot, "symbols", variant.BaseLayoutId)));
                Assert.True(File.Exists(Path.Combine(bundleRoot, "rules", "evdev.xml")));
            });
            return Task.FromResult(Result(FailVariantCall == VariantRoots.Count));
        }

        public Task<XkbUserBundleVerificationResult> VerifyBaseAsync(
            string bundleRoot,
            XkbUserVariantMetadata removedVariant,
            XkbUserInstallCapability capability,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result(failed: false));
        }

        private static XkbUserBundleVerificationResult Result(bool failed) => new(
            failed
                ? XkbUserBundleVerificationStatus.Failed
                : XkbUserBundleVerificationStatus.Verified,
            "/usr/bin/xkbcli",
            "xkbcli 1.13.1",
            [],
            failed ? [new XkbDiagnostic("TEST", "Injected verification failure.")] : []);
    }

    private sealed class ThrowingObserver(
        XkbInstallTransactionStep target,
        int occurrence,
        bool crash) : IXkbInstallTransactionObserver
    {
        private int _seen;

        public void OnStep(
            XkbInstallTransactionStep milestone,
            string transactionId,
            string? relativePath)
        {
            if (milestone != target || ++_seen != occurrence)
            {
                return;
            }

            if (crash)
            {
                throw new SimulatedCrashException();
            }

            throw new IOException($"Injected failure after {milestone}.");
        }
    }

    private sealed class CancellingObserver(CancellationTokenSource source)
        : IXkbInstallTransactionObserver
    {
        public void OnStep(
            XkbInstallTransactionStep milestone,
            string transactionId,
            string? relativePath)
        {
            if (milestone == XkbInstallTransactionStep.DestinationApplied)
            {
                source.Cancel();
            }
        }
    }

    private sealed class SimulatedCrashException : Exception;
}
