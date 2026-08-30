using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbInstallationManifestSerializerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void SerializeAndDeserialize_PreservesHostLocalOwnershipState()
    {
        var instant = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        var manifest = new XkbInstallationManifest(
            XkbInstallationManifest.CurrentSchemaVersion,
            [
                new XkbInstalledVariant(
                    "7c31d5f2a19e40a4b0ef64f01a295135",
                    "pl",
                    "qwertz",
                    "qwertz",
                    "keyboardstudio_programmer",
                    "ks_7c31d5f2a19e",
                    "Polish - KeyboardStudio",
                    new string('a', 64),
                    new string('b', 64),
                    new string('c', 64),
                    instant,
                    instant,
                    "xkbcli 1.13.1")
            ],
            [new XkbManagedFileRecord("symbols/keyboardstudio", new string('d', 64), true)]);

        var json = XkbInstallationManifestSerializer.Serialize(manifest);
        var loaded = XkbInstallationManifestSerializer.Deserialize(json);

        Assert.Equal(manifest.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal(manifest.Installations, loaded.Installations);
        Assert.Equal(manifest.Files, loaded.Files);
        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.EndsWith("\n", json);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Deserialize_WhenSchemaIsUnknown_RejectsManifest()
    {
        Assert.Throws<InvalidDataException>(() =>
            XkbInstallationManifestSerializer.Deserialize(
                "{\"schemaVersion\":2,\"installations\":[],\"files\":[]}"));
    }
}
