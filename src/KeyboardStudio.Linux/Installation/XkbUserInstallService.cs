using System.Security.Cryptography;
using System.Text;

namespace KeyboardStudio.Linux;

/// <summary>Executes ownership-aware XKB plans with verification, journaling, and rollback.</summary>
public sealed class XkbUserInstallService : IXkbUserInstallService
{
    private const string ManifestFileName = "installations.json";
    private const string JournalFileName = "journal.json";

    private readonly IXkbUserBundleVerifier _verifier;
    private readonly TimeProvider _timeProvider;
    private readonly IXkbInstallTransactionObserver _observer;

    public XkbUserInstallService()
        : this(new XkbUserBundleVerifier(), TimeProvider.System, NullXkbInstallTransactionObserver.Instance)
    {
    }

    public XkbUserInstallService(
        IXkbUserBundleVerifier verifier,
        TimeProvider? timeProvider = null,
        IXkbInstallTransactionObserver? observer = null)
    {
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _observer = observer ?? NullXkbInstallTransactionObserver.Instance;
    }

    public async Task<XkbUserInstallResult> InstallOrUpdateAsync(
        XkbGeneratedUserBundle bundle,
        XkbUserVariantMetadata metadata,
        XdgDirectoryPaths paths,
        XkbUserInstallCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(capability);

        var readiness = ValidateManagedOperation(paths, capability);
        if (readiness.Count > 0)
        {
            return Failed(XkbUserInstallCommand.Install, readiness);
        }

        var recovery = await RecoverAsync(paths, cancellationToken);
        if (!recovery.Success)
        {
            return Failed(XkbUserInstallCommand.Install, recovery.Diagnostics);
        }

        var state = await ReadStateAsync(paths, AdditionalPaths(metadata), cancellationToken);
        if (!state.Success)
        {
            return Failed(XkbUserInstallCommand.Install, state.Diagnostics, recovery.Recovered);
        }

        var planResult = XkbInstallPlanner.PlanInstall(
            bundle,
            metadata,
            paths,
            state.Manifest!,
            state.Snapshots!,
            _timeProvider.GetUtcNow(),
            capability.XkbCliVersionOutput);
        if (!planResult.Success)
        {
            return Failed(XkbUserInstallCommand.Install, planResult.Diagnostics, recovery.Recovered);
        }

        var command = planResult.Plan!.Action == XkbInstallAction.Install
            ? XkbUserInstallCommand.Install
            : XkbUserInstallCommand.Update;
        return await ExecutePlanAsync(
            planResult.Plan,
            paths,
            capability,
            removedVariant: null,
            command,
            recovery.Recovered,
            cancellationToken);
    }

    public async Task<XkbUserInstallResult> VerifyInstalledAsync(
        string projectInstallationId,
        XdgDirectoryPaths paths,
        XkbUserInstallCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectInstallationId);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(capability);

        var readiness = ValidateManagedOperation(paths, capability);
        if (readiness.Count > 0)
        {
            return Failed(XkbUserInstallCommand.VerifyInstalled, readiness);
        }

        var recovery = await RecoverAsync(paths, cancellationToken);
        if (!recovery.Success)
        {
            return Failed(XkbUserInstallCommand.VerifyInstalled, recovery.Diagnostics);
        }

        var state = await ReadStateAsync(paths, [], cancellationToken);
        if (!state.Success)
        {
            return Failed(
                XkbUserInstallCommand.VerifyInstalled,
                state.Diagnostics,
                recovery.Recovered);
        }

        var installed = state.Manifest!.Installations.SingleOrDefault(item =>
            string.Equals(item.ProjectInstallationId, projectInstallationId, StringComparison.Ordinal));
        if (installed is null)
        {
            return Failed(
                XkbUserInstallCommand.VerifyInstalled,
                [new XkbDiagnostic("KSI005", "The selected project is not installed.")],
                recovery.Recovered);
        }

        var ownershipDiagnostics = ValidateInstalledOwnership(paths, state.Manifest);
        if (ownershipDiagnostics.Count > 0)
        {
            return Failed(
                XkbUserInstallCommand.VerifyInstalled,
                ownershipDiagnostics,
                recovery.Recovered);
        }

        var verification = await _verifier.VerifyAsync(
            paths.UserXkbRoot,
            state.Manifest.Installations.Select(ToMetadata).ToArray(),
            capability,
            requireBundleManifest: false,
            cancellationToken);
        var success = verification.Status != XkbUserBundleVerificationStatus.Failed;
        return new XkbUserInstallResult(
            success,
            XkbUserInstallCommand.VerifyInstalled,
            state.Manifest,
            null,
            verification,
            recovery.Recovered,
            RolledBack: false,
            verification.Diagnostics);
    }

    public async Task<XkbUserInstallResult> UninstallAsync(
        string projectInstallationId,
        XdgDirectoryPaths paths,
        XkbUserInstallCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectInstallationId);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(capability);

        var readiness = ValidateManagedOperation(paths, capability);
        if (readiness.Count > 0)
        {
            return Failed(XkbUserInstallCommand.Uninstall, readiness);
        }

        var recovery = await RecoverAsync(paths, cancellationToken);
        if (!recovery.Success)
        {
            return Failed(XkbUserInstallCommand.Uninstall, recovery.Diagnostics);
        }

        var state = await ReadStateAsync(paths, [], cancellationToken);
        if (!state.Success)
        {
            return Failed(XkbUserInstallCommand.Uninstall, state.Diagnostics, recovery.Recovered);
        }

        var installed = state.Manifest!.Installations.SingleOrDefault(item =>
            string.Equals(item.ProjectInstallationId, projectInstallationId, StringComparison.Ordinal));
        if (installed is null)
        {
            return Failed(
                XkbUserInstallCommand.Uninstall,
                [new XkbDiagnostic("KSI005", "The selected project is not installed.")],
                recovery.Recovered);
        }

        var planResult = XkbInstallPlanner.PlanUninstall(
            projectInstallationId,
            paths,
            state.Manifest,
            state.Snapshots!);
        if (!planResult.Success)
        {
            return Failed(
                XkbUserInstallCommand.Uninstall,
                planResult.Diagnostics,
                recovery.Recovered);
        }

        return await ExecutePlanAsync(
            planResult.Plan!,
            paths,
            capability,
            ToMetadata(installed),
            XkbUserInstallCommand.Uninstall,
            recovery.Recovered,
            cancellationToken);
    }

    public async Task<XkbUserRecoveryResult> RecoverAsync(
        XdgDirectoryPaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var safety = ValidatePaths(paths);
        if (safety.Count > 0)
        {
            return new XkbUserRecoveryResult(false, false, null, safety);
        }

        var journalPath = Path.Combine(paths.KeyboardStudioStateRoot, JournalFileName);
        if (!File.Exists(journalPath))
        {
            return new XkbUserRecoveryResult(true, false, null, []);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNoSymlink(journalPath, paths.KeyboardStudioStateRoot);
            var journal = XkbTransactionJournalSerializer.Deserialize(
                await File.ReadAllTextAsync(journalPath, cancellationToken));
            if (!string.Equals(
                    Path.GetFullPath(journal.UserXkbRoot),
                    Path.GetFullPath(paths.UserXkbRoot),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    Path.GetFullPath(journal.StateRoot),
                    Path.GetFullPath(paths.KeyboardStudioStateRoot),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("The interrupted transaction belongs to different XDG roots.");
            }

            await RestoreAsync(paths, journal, cancellationToken);
            File.Delete(journalPath);
            _observer.OnStep(XkbInstallTransactionStep.RecoveryCompleted, journal.TransactionId, null);
            CleanupTransaction(paths, journal.TransactionId);
            return new XkbUserRecoveryResult(true, true, journal.TransactionId, []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsFileSystemOrDataException(exception))
        {
            return new XkbUserRecoveryResult(
                false,
                false,
                null,
                [new XkbDiagnostic("KSI009", $"Interrupted XKB transaction recovery failed: {exception.Message}")]);
        }
    }

    private async Task<XkbUserInstallResult> ExecutePlanAsync(
        XkbInstallPlan plan,
        XdgDirectoryPaths paths,
        XkbUserInstallCapability capability,
        XkbUserVariantMetadata? removedVariant,
        XkbUserInstallCommand command,
        bool recovered,
        CancellationToken cancellationToken)
    {
        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = TransactionRoot(paths, transactionId);
        var proposedRoot = Path.Combine(transactionRoot, "proposed");
        XkbUserBundleVerificationResult? proposedVerification = null;
        XkbTransactionJournal? journal = null;
        var liveMutationStarted = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNoSymlink(transactionRoot, paths.KeyboardStudioStateRoot);
            EnsureNoSymlink(BackupRoot(paths, transactionId), paths.KeyboardStudioStateRoot);
            PrepareProposedRoot(paths.UserXkbRoot, proposedRoot, plan.Operations);
            _observer.OnStep(XkbInstallTransactionStep.ProposedRootPrepared, transactionId, null);

            proposedVerification = await VerifyResultAsync(
                proposedRoot,
                plan.UpdatedManifest,
                removedVariant,
                capability,
                cancellationToken);
            if (proposedVerification.Status == XkbUserBundleVerificationStatus.Failed)
            {
                CleanupTransaction(paths, transactionId);
                return new XkbUserInstallResult(
                    false,
                    command,
                    null,
                    proposedVerification,
                    null,
                    recovered,
                    RolledBack: false,
                    proposedVerification.Diagnostics);
            }

            _observer.OnStep(XkbInstallTransactionStep.ProposedRootVerified, transactionId, null);
            journal = await CreateBackupsAsync(plan, paths, transactionId, cancellationToken);
            _observer.OnStep(XkbInstallTransactionStep.BackupsPrepared, transactionId, null);
            await AtomicWriteTextAsync(
                Path.Combine(paths.KeyboardStudioStateRoot, JournalFileName),
                XkbTransactionJournalSerializer.Serialize(journal),
                transactionId,
                cancellationToken);
            liveMutationStarted = true;
            _observer.OnStep(XkbInstallTransactionStep.JournalWritten, transactionId, null);

            foreach (var operation in plan.Operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ApplyLiveOperation(operation, paths, transactionId);
                _observer.OnStep(
                    XkbInstallTransactionStep.DestinationApplied,
                    transactionId,
                    operation.RelativePath);
            }

            ValidateAppliedOperations(plan.Operations);
            var installedVerification = await VerifyResultAsync(
                paths.UserXkbRoot,
                plan.UpdatedManifest,
                removedVariant,
                capability,
                cancellationToken);
            if (installedVerification.Status == XkbUserBundleVerificationStatus.Failed)
            {
                throw new InvalidDataException("The installed XKB result failed post-install verification.");
            }

            _observer.OnStep(XkbInstallTransactionStep.InstalledRootVerified, transactionId, null);
            await AtomicWriteTextAsync(
                plan.ManifestPath,
                XkbInstallationManifestSerializer.Serialize(plan.UpdatedManifest),
                transactionId,
                cancellationToken);
            _observer.OnStep(XkbInstallTransactionStep.ManifestWritten, transactionId, null);

            File.Delete(Path.Combine(paths.KeyboardStudioStateRoot, JournalFileName));
            liveMutationStarted = false;
            _observer.OnStep(XkbInstallTransactionStep.JournalCleared, transactionId, null);
            CleanupTransaction(paths, transactionId);
            return new XkbUserInstallResult(
                true,
                command,
                plan.UpdatedManifest,
                proposedVerification,
                installedVerification,
                recovered,
                RolledBack: false,
                proposedVerification.Diagnostics.Concat(installedVerification.Diagnostics).ToArray());
        }
        catch (OperationCanceledException)
        {
            if (liveMutationStarted && journal is not null)
            {
                await RollbackAsync(paths, journal, transactionId);
            }
            else
            {
                CleanupTransaction(paths, transactionId);
            }

            throw;
        }
        catch (Exception exception) when (IsFileSystemOrDataException(exception))
        {
            var rolledBack = false;
            var diagnostics = new List<XkbDiagnostic>
            {
                new("KSI008", $"The XKB transaction failed: {exception.Message}")
            };
            if (liveMutationStarted && journal is not null)
            {
                try
                {
                    await RollbackAsync(paths, journal, transactionId);
                    rolledBack = true;
                }
                catch (Exception rollbackException) when (IsFileSystemOrDataException(rollbackException))
                {
                    diagnostics.Add(new XkbDiagnostic(
                        "KSI009",
                        $"Automatic rollback failed; the journal was retained for recovery: {rollbackException.Message}"));
                }
            }
            else
            {
                CleanupTransaction(paths, transactionId);
            }

            return new XkbUserInstallResult(
                false,
                command,
                null,
                proposedVerification,
                null,
                recovered,
                rolledBack,
                diagnostics);
        }
    }

    private async Task<XkbUserBundleVerificationResult> VerifyResultAsync(
        string root,
        XkbInstallationManifest manifest,
        XkbUserVariantMetadata? removedVariant,
        XkbUserInstallCapability capability,
        CancellationToken cancellationToken)
    {
        var results = new List<XkbUserBundleVerificationResult>();
        if (manifest.Installations.Count > 0)
        {
            results.Add(await _verifier.VerifyAsync(
                root,
                manifest.Installations.Select(ToMetadata).ToArray(),
                capability,
                requireBundleManifest: false,
                cancellationToken));
        }

        if (removedVariant is not null)
        {
            results.Add(await _verifier.VerifyBaseAsync(
                root,
                removedVariant,
                capability,
                cancellationToken));
        }

        return CombineVerification(results, capability);
    }

    private static XkbUserBundleVerificationResult CombineVerification(
        List<XkbUserBundleVerificationResult> results,
        XkbUserInstallCapability capability)
    {
        if (results.Count == 0)
        {
            return new XkbUserBundleVerificationResult(
                XkbUserBundleVerificationStatus.Verified,
                capability.XkbCliPath,
                capability.XkbCliVersionOutput,
                [],
                []);
        }

        var status = results.Any(result => result.Status == XkbUserBundleVerificationStatus.Failed)
            ? XkbUserBundleVerificationStatus.Failed
            : results.Any(result => result.Status == XkbUserBundleVerificationStatus.VerifiedWithWarnings)
                ? XkbUserBundleVerificationStatus.VerifiedWithWarnings
                : XkbUserBundleVerificationStatus.Verified;
        return new XkbUserBundleVerificationResult(
            status,
            results.Select(result => result.ToolPath).FirstOrDefault(path => path is not null),
            results.Select(result => result.ToolVersion).FirstOrDefault(version => version is not null),
            results.SelectMany(result => result.Checks).ToArray(),
            results.SelectMany(result => result.Diagnostics).ToArray());
    }

    private static async Task<ReadStateResult> ReadStateAsync(
        XdgDirectoryPaths paths,
        IReadOnlyList<string> additionalPaths,
        CancellationToken cancellationToken)
    {
        try
        {
            var manifestPath = Path.Combine(paths.KeyboardStudioStateRoot, ManifestFileName);
            EnsureNoSymlink(manifestPath, paths.KeyboardStudioStateRoot);
            var manifest = File.Exists(manifestPath)
                ? XkbInstallationManifestSerializer.Deserialize(
                    await File.ReadAllTextAsync(manifestPath, cancellationToken))
                : XkbInstallationManifest.Empty;
            var relativePaths = manifest.Files.Select(file => file.RelativePath)
                .Concat(additionalPaths)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var snapshots = new List<XkbInstallFileSnapshot>(relativePaths.Length);
            foreach (var relativePath in relativePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Destination(paths.UserXkbRoot, relativePath);
                EnsureNoSymlink(path, paths.UserXkbRoot);
                snapshots.Add(new XkbInstallFileSnapshot(
                    relativePath,
                    File.Exists(path)
                        ? await File.ReadAllTextAsync(path, cancellationToken)
                        : null,
                    IsSymbolicLink: false));
            }

            return new ReadStateResult(true, manifest, snapshots, []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsFileSystemOrDataException(exception))
        {
            return new ReadStateResult(
                false,
                null,
                null,
                [new XkbDiagnostic("KSI004", $"The current XKB installation state could not be read: {exception.Message}")]);
        }
    }

    private async Task<XkbTransactionJournal> CreateBackupsAsync(
        XkbInstallPlan plan,
        XdgDirectoryPaths paths,
        string transactionId,
        CancellationToken cancellationToken)
    {
        var backupRoot = BackupRoot(paths, transactionId);
        EnsureNoSymlink(backupRoot, paths.KeyboardStudioStateRoot);
        Directory.CreateDirectory(backupRoot);
        var files = new List<XkbTransactionFileBackup>(plan.Operations.Count);
        foreach (var operation in plan.Operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNoSymlink(operation.DestinationPath, paths.UserXkbRoot);
            var existed = File.Exists(operation.DestinationPath);
            string? hash = null;
            if (existed)
            {
                hash = HashFile(operation.DestinationPath);
                var backup = Destination(Path.Combine(backupRoot, "files"), operation.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Copy(operation.DestinationPath, backup, overwrite: false);
            }

            files.Add(new XkbTransactionFileBackup(operation.RelativePath, existed, hash));
        }

        var manifestPath = plan.ManifestPath;
        EnsureNoSymlink(manifestPath, paths.KeyboardStudioStateRoot);
        var manifestExisted = File.Exists(manifestPath);
        string? manifestHash = null;
        if (manifestExisted)
        {
            manifestHash = HashFile(manifestPath);
            File.Copy(manifestPath, Path.Combine(backupRoot, ManifestFileName), overwrite: false);
        }

        return new XkbTransactionJournal(
            XkbTransactionJournal.CurrentSchemaVersion,
            transactionId,
            plan.Action,
            plan.ProjectInstallationId,
            Path.GetFullPath(paths.UserXkbRoot),
            Path.GetFullPath(paths.KeyboardStudioStateRoot),
            _timeProvider.GetUtcNow(),
            files,
            manifestExisted,
            manifestHash);
    }

    private async Task RollbackAsync(
        XdgDirectoryPaths paths,
        XkbTransactionJournal journal,
        string transactionId)
    {
        await RestoreAsync(paths, journal, CancellationToken.None);
        File.Delete(Path.Combine(paths.KeyboardStudioStateRoot, JournalFileName));
        _observer.OnStep(XkbInstallTransactionStep.RollbackCompleted, transactionId, null);
        CleanupTransaction(paths, transactionId);
    }

    private static async Task RestoreAsync(
        XdgDirectoryPaths paths,
        XkbTransactionJournal journal,
        CancellationToken cancellationToken)
    {
        var backupRoot = BackupRoot(paths, journal.TransactionId);
        EnsureNoSymlink(backupRoot, paths.KeyboardStudioStateRoot);
        foreach (var file in journal.Files.Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = Destination(paths.UserXkbRoot, file.RelativePath);
            EnsureNoSymlink(destination, paths.UserXkbRoot, allowFinalLink: true);
            if (file.Existed)
            {
                var backup = Destination(Path.Combine(backupRoot, "files"), file.RelativePath);
                EnsureNoSymlink(backup, backupRoot);
                if (!File.Exists(backup) ||
                    !string.Equals(HashFile(backup), file.Sha256, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Backup '{file.RelativePath}' is missing or corrupt.");
                }

                await AtomicCopyAsync(backup, destination, journal.TransactionId, cancellationToken);
            }
            else if (File.Exists(destination) || IsSymbolicLink(destination))
            {
                File.Delete(destination);
            }

            var tombstone = destination + $".keyboardstudio-{journal.TransactionId}.deleted";
            if (File.Exists(tombstone))
            {
                File.Delete(tombstone);
            }
        }

        var manifestPath = Path.Combine(paths.KeyboardStudioStateRoot, ManifestFileName);
        EnsureNoSymlink(manifestPath, paths.KeyboardStudioStateRoot, allowFinalLink: true);
        if (journal.ManifestExisted)
        {
            var backup = Path.Combine(backupRoot, ManifestFileName);
            EnsureNoSymlink(backup, backupRoot);
            if (!File.Exists(backup) ||
                !string.Equals(HashFile(backup), journal.ManifestSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The installation manifest backup is missing or corrupt.");
            }

            await AtomicCopyAsync(backup, manifestPath, journal.TransactionId, cancellationToken);
        }
        else if (File.Exists(manifestPath) || IsSymbolicLink(manifestPath))
        {
            File.Delete(manifestPath);
        }
    }

    private static void PrepareProposedRoot(
        string liveRoot,
        string proposedRoot,
        IReadOnlyList<XkbInstallOperation> operations)
    {
        Directory.CreateDirectory(proposedRoot);
        if (Directory.Exists(liveRoot))
        {
            EnsureNoSymlink(liveRoot, liveRoot);
            CopyTreeWithoutLinks(liveRoot, liveRoot, proposedRoot);
        }

        foreach (var operation in operations)
        {
            var destination = Destination(proposedRoot, operation.RelativePath);
            if (operation.Kind == XkbInstallOperationKind.Delete)
            {
                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllText(destination, operation.Content!, new UTF8Encoding(false));
            }
        }
    }

    private static void CopyTreeWithoutLinks(
        string source,
        string sourceRoot,
        string destinationRoot)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            if (IsSymbolicLink(entry))
            {
                throw new InvalidDataException($"The user XKB root contains symbolic link '{entry}'.");
            }

            var relative = Portable(Path.GetRelativePath(sourceRoot, entry));
            var target = Destination(destinationRoot, relative);
            if (Directory.Exists(entry))
            {
                Directory.CreateDirectory(target);
                CopyTreeWithoutLinks(entry, sourceRoot, destinationRoot);
            }
            else if (File.Exists(entry))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(entry, target, overwrite: false);
            }
            else
            {
                throw new InvalidDataException($"Unsupported filesystem entry '{entry}' exists in the user XKB root.");
            }
        }
    }

    private static void ApplyLiveOperation(
        XkbInstallOperation operation,
        XdgDirectoryPaths paths,
        string transactionId)
    {
        EnsureNoSymlink(operation.DestinationPath, paths.UserXkbRoot);
        var exists = File.Exists(operation.DestinationPath);
        if (operation.ExpectedExistingSha256 is null)
        {
            if (exists)
            {
                throw new InvalidDataException($"Destination '{operation.RelativePath}' appeared after planning.");
            }
        }
        else if (!exists ||
                 !string.Equals(HashFile(operation.DestinationPath), operation.ExpectedExistingSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Destination '{operation.RelativePath}' changed after planning.");
        }

        if (operation.Kind == XkbInstallOperationKind.Delete)
        {
            var tombstone = operation.DestinationPath + $".keyboardstudio-{transactionId}.deleted";
            File.Move(operation.DestinationPath, tombstone, overwrite: false);
            File.Delete(tombstone);
            return;
        }

        AtomicWriteTextAsync(
            operation.DestinationPath,
            operation.Content!,
            transactionId,
            CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void ValidateAppliedOperations(IReadOnlyList<XkbInstallOperation> operations)
    {
        foreach (var operation in operations)
        {
            if (operation.Kind == XkbInstallOperationKind.Delete)
            {
                if (File.Exists(operation.DestinationPath))
                {
                    throw new InvalidDataException($"Destination '{operation.RelativePath}' was not deleted.");
                }
            }
            else if (!File.Exists(operation.DestinationPath) ||
                     !string.Equals(HashFile(operation.DestinationPath), operation.ContentSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Destination '{operation.RelativePath}' does not match its planned hash.");
            }
        }
    }

    private static List<XkbDiagnostic> ValidateInstalledOwnership(
        XdgDirectoryPaths paths,
        XkbInstallationManifest manifest)
    {
        var diagnostics = new List<XkbDiagnostic>();
        var centralRecord = manifest.Files.SingleOrDefault(file =>
            string.Equals(file.RelativePath, "symbols/keyboardstudio", StringComparison.Ordinal));
        var centralPath = Destination(paths.UserXkbRoot, "symbols/keyboardstudio");
        if (centralRecord is null || !File.Exists(centralPath) ||
            !string.Equals(HashFile(centralPath), centralRecord.Sha256, StringComparison.Ordinal))
        {
            diagnostics.Add(new XkbDiagnostic("KSI006", "The app-owned central symbols file is missing or modified."));
            return diagnostics;
        }

        var central = File.ReadAllText(centralPath);
        var registryPath = Destination(paths.UserXkbRoot, "rules/evdev.xml");
        if (!File.Exists(registryPath))
        {
            diagnostics.Add(new XkbDiagnostic("KSI006", "The managed registry file is missing."));
            return diagnostics;
        }

        var registry = File.ReadAllText(registryPath);
        foreach (var installed in manifest.Installations)
        {
            var centralBlock = XkbManagedBlockEditor.Read(central, installed.ProjectInstallationId);
            var bridgePath = Destination(paths.UserXkbRoot, $"symbols/{installed.BaseLayoutId}");
            var bridgeBlock = File.Exists(bridgePath)
                ? XkbManagedBlockEditor.Read(File.ReadAllText(bridgePath), installed.ProjectInstallationId)
                : new XkbManagedBlockEditResult(false, null, null, false, []);
            var registryEntry = XkbRegistryDocumentMerger.Upsert(
                registry,
                ToMetadata(installed),
                installed.RegistryEntrySha256);
            if (!centralBlock.Success ||
                !string.Equals(centralBlock.ManagedBlockSha256, installed.CentralBlockSha256, StringComparison.Ordinal) ||
                !bridgeBlock.Success ||
                !string.Equals(bridgeBlock.ManagedBlockSha256, installed.BridgeBlockSha256, StringComparison.Ordinal) ||
                !registryEntry.Success)
            {
                diagnostics.Add(new XkbDiagnostic(
                    "KSI006",
                    $"Installed variant '{installed.BaseLayoutId}({installed.PublicVariantId})' is missing or externally modified."));
            }
        }

        return diagnostics;
    }

    private static async Task AtomicWriteTextAsync(
        string destination,
        string content,
        string transactionId,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + $".keyboardstudio-{transactionId}.tmp";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            await using (var output = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await output.WriteAsync(bytes, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }

            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task AtomicCopyAsync(
        string source,
        string destination,
        string transactionId,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + $".keyboardstudio-{transactionId}.restore";
        try
        {
            await using (var input = File.OpenRead(source))
            await using (var output = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static IReadOnlyList<string> AdditionalPaths(XkbUserVariantMetadata metadata) =>
        ["symbols/keyboardstudio", $"symbols/{metadata.BaseLayoutId}", "rules/evdev.xml"];

    private static XkbUserVariantMetadata ToMetadata(XkbInstalledVariant installed) =>
        new(
            installed.ProjectInstallationId,
            installed.BaseLayoutId,
            installed.BaseVariantId,
            installed.ResolvedBaseSectionId,
            installed.PublicVariantId,
            installed.Description);

    private static List<XkbDiagnostic> ValidateManagedOperation(
        XdgDirectoryPaths paths,
        XkbUserInstallCapability capability)
    {
        var diagnostics = ValidatePaths(paths);
        if (capability.Mode != XkbUserInstallMode.ManagedInstallation ||
            !capability.PathsAreSafe ||
            !string.Equals(capability.UserXkbRoot, paths.UserXkbRoot, StringComparison.Ordinal) ||
            !string.Equals(capability.StateRoot, paths.KeyboardStudioStateRoot, StringComparison.Ordinal))
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSI001",
                "Managed installation is unavailable or the probed XDG paths no longer match."));
        }

        return diagnostics;
    }

    private static List<XkbDiagnostic> ValidatePaths(XdgDirectoryPaths paths)
    {
        return XdgDirectoryPathValidator.IsSafe(paths)
            ? []
            : [new XkbDiagnostic("KSI002", "The resolved XDG paths are unsafe.")];
    }

    private static string Destination(string root, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine([root, .. relativePath.Split('/')]));
        if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Path '{relativePath}' escapes its managed root.");
        }

        return path;
    }

    private static void EnsureNoSymlink(string path, string root, bool allowFinalLink = false)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(path);
        if (fullPath != fullRoot &&
            !fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Path '{path}' escapes its managed root.");
        }

        var current = fullRoot;
        if (IsSymbolicLink(current))
        {
            throw new InvalidDataException($"Managed root '{fullRoot}' is a symbolic link.");
        }

        EnsureSecureDirectory(current);

        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (relative == ".")
        {
            return;
        }

        var segments = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            current = Path.Combine(current, segments[index]);
            if (allowFinalLink && index == segments.Length - 1)
            {
                continue;
            }

            if (IsSymbolicLink(current))
            {
                throw new InvalidDataException($"Managed path component '{current}' is a symbolic link.");
            }

            EnsureSecureDirectory(current);
        }
    }

    private static void EnsureSecureDirectory(string path)
    {
        if (!Directory.Exists(path) || OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        if ((mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
        {
            throw new InvalidDataException($"Managed directory '{path}' is writable by another user or group.");
        }
    }

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

    private static string Portable(string relativePath) =>
        relativePath.Replace(Path.DirectorySeparatorChar, '/');

    private static string TransactionRoot(XdgDirectoryPaths paths, string transactionId) =>
        Path.Combine(paths.KeyboardStudioStateRoot, "transactions", transactionId);

    private static string BackupRoot(XdgDirectoryPaths paths, string transactionId) =>
        Path.Combine(paths.KeyboardStudioStateRoot, "backups", transactionId);

    private static void CleanupTransaction(XdgDirectoryPaths paths, string transactionId)
    {
        var transactionRoot = TransactionRoot(paths, transactionId);
        if (Directory.Exists(transactionRoot))
        {
            EnsureNoSymlink(transactionRoot, paths.KeyboardStudioStateRoot);
            Directory.Delete(transactionRoot, recursive: true);
        }

        var backupRoot = BackupRoot(paths, transactionId);
        if (Directory.Exists(backupRoot))
        {
            EnsureNoSymlink(backupRoot, paths.KeyboardStudioStateRoot);
            Directory.Delete(backupRoot, recursive: true);
        }

        DeleteIfEmpty(Path.Combine(paths.KeyboardStudioStateRoot, "transactions"), paths.KeyboardStudioStateRoot);
        DeleteIfEmpty(Path.Combine(paths.KeyboardStudioStateRoot, "backups"), paths.KeyboardStudioStateRoot);
    }

    private static void DeleteIfEmpty(string path, string stateRoot)
    {
        if (Directory.Exists(path))
        {
            EnsureNoSymlink(path, stateRoot);
            if (!Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
    }

    private static string HashFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static bool IsFileSystemOrDataException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException;

    private static XkbUserInstallResult Failed(
        XkbUserInstallCommand command,
        IReadOnlyList<XkbDiagnostic> diagnostics,
        bool recovered = false) =>
        new(false, command, null, null, null, recovered, RolledBack: false, diagnostics);

    private sealed record ReadStateResult(
        bool Success,
        XkbInstallationManifest? Manifest,
        IReadOnlyList<XkbInstallFileSnapshot>? Snapshots,
        IReadOnlyList<XkbDiagnostic> Diagnostics);
}
