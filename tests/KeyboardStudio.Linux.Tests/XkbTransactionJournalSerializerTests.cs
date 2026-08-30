using KeyboardStudio.Linux;
using System.Text.Json;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbTransactionJournalSerializerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void SerializeAndDeserialize_PreservesRecoveryState()
    {
        var hash = new string('a', 64);
        var journal = new XkbTransactionJournal(
            XkbTransactionJournal.CurrentSchemaVersion,
            "1c31d5f2a19e40a4b0ef64f01a295135",
            XkbInstallAction.Update,
            "7c31d5f2a19e40a4b0ef64f01a295135",
            "/home/test/.config/xkb",
            "/home/test/.local/state/keyboardstudio/xkb",
            new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero),
            [new XkbTransactionFileBackup("symbols/pl", true, hash)],
            ManifestExisted: true,
            hash);

        var restored = XkbTransactionJournalSerializer.Deserialize(
            XkbTransactionJournalSerializer.Serialize(journal));

        Assert.Equal(journal.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(journal.TransactionId, restored.TransactionId);
        Assert.Equal(journal.Action, restored.Action);
        Assert.Equal(journal.ProjectInstallationId, restored.ProjectInstallationId);
        Assert.Equal(journal.UserXkbRoot, restored.UserXkbRoot);
        Assert.Equal(journal.StateRoot, restored.StateRoot);
        Assert.Equal(journal.StartedAtUtc, restored.StartedAtUtc);
        Assert.Equal(journal.ManifestExisted, restored.ManifestExisted);
        Assert.Equal(journal.ManifestSha256, restored.ManifestSha256);
        Assert.Equal(journal.Files, restored.Files);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    [InlineData("../symbols/pl")]
    [InlineData("/etc/xkb")]
    [InlineData("symbols\\pl")]
    public void Deserialize_RejectsUnsafeBackupPaths(string path)
    {
        var json = $$"""
            {
              "schemaVersion": 1,
              "transactionId": "1c31d5f2a19e40a4b0ef64f01a295135",
              "action": 0,
              "projectInstallationId": "7c31d5f2a19e40a4b0ef64f01a295135",
              "userXkbRoot": "/home/test/.config/xkb",
              "stateRoot": "/home/test/.local/state/keyboardstudio/xkb",
              "startedAtUtc": "2026-08-30T10:00:00+00:00",
              "files": [{ "relativePath": {{JsonSerializer.Serialize(path)}}, "existed": false, "sha256": null }],
              "manifestExisted": false,
              "manifestSha256": null
            }
            """;

        Assert.Throws<InvalidDataException>(() =>
            XkbTransactionJournalSerializer.Deserialize(json));
    }
}
