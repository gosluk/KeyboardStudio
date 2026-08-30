using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbManagedBlockEditorTests
{
    private const string FirstId = "7c31d5f2a19e40a4b0ef64f01a295135";
    private const string SecondId = "8d42e6a3b20f41b5c1f075a12b306246";

    [Fact]
    [Trait("Category", "Unit")]
    public void Upsert_IntoHandWrittenFile_PreservesEveryExistingByte()
    {
        const string existing = "// mine\nxkb_symbols \"personal\" { include \"%S/pl(basic)\" };\n";

        var result = XkbManagedBlockEditor.Upsert(
            existing,
            FirstId,
            "keyboardstudio_one",
            Block(FirstId, "keyboardstudio_one"),
            expectedExistingBlockSha256: null);

        Assert.True(result.Success);
        Assert.StartsWith(existing, result.Content);
        Assert.Contains($"// BEGIN KeyboardStudio {FirstId}", result.Content);
        Assert.NotNull(result.ManagedBlockSha256);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Upsert_AndRemoveSeveralProjects_PreservesTheOtherManagedBlock()
    {
        var first = XkbManagedBlockEditor.Upsert(
            string.Empty,
            FirstId,
            "keyboardstudio_one",
            Block(FirstId, "keyboardstudio_one"),
            null);
        var second = XkbManagedBlockEditor.Upsert(
            first.Content!,
            SecondId,
            "keyboardstudio_two",
            Block(SecondId, "keyboardstudio_two"),
            null);

        var removed = XkbManagedBlockEditor.Remove(
            second.Content!,
            FirstId,
            first.ManagedBlockSha256!);

        Assert.True(removed.Success);
        Assert.DoesNotContain(FirstId, removed.Content);
        Assert.Contains(SecondId, removed.Content);
        var last = XkbManagedBlockEditor.Remove(
            removed.Content!,
            SecondId,
            second.ManagedBlockSha256!);
        Assert.Null(last.Content);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Upsert_ExistingOwnedBlock_ReplacesOnlyThatBlock()
    {
        var initial = XkbManagedBlockEditor.Upsert(
            "// unrelated\n",
            FirstId,
            "keyboardstudio_one",
            Block(FirstId, "keyboardstudio_one"),
            null);

        var updated = XkbManagedBlockEditor.Upsert(
            initial.Content!,
            FirstId,
            "keyboardstudio_one",
            Block(FirstId, "keyboardstudio_one", "    // updated\n"),
            initial.ManagedBlockSha256);

        Assert.True(updated.Success);
        Assert.StartsWith("// unrelated\n", updated.Content);
        Assert.Contains("// updated", updated.Content);
        Assert.NotEqual(initial.ManagedBlockSha256, updated.ManagedBlockSha256);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Upsert_WhenManagedBlockWasExternallyChanged_RefusesReplacement()
    {
        var initial = XkbManagedBlockEditor.Upsert(
            string.Empty,
            FirstId,
            "keyboardstudio_one",
            Block(FirstId, "keyboardstudio_one"),
            null);
        var changed = initial.Content!.Replace("include", "// external\n    include", StringComparison.Ordinal);

        var result = XkbManagedBlockEditor.Upsert(
            changed,
            FirstId,
            "keyboardstudio_one",
            Block(FirstId, "keyboardstudio_one"),
            initial.ManagedBlockSha256);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSM003");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Upsert_WhenPublicSectionAlreadyExistsOutsideManagedBlock_RefusesCollision()
    {
        const string existing = "xkb_symbols \"keyboardstudio_one\" { };\n";

        var result = XkbManagedBlockEditor.Upsert(
            existing,
            FirstId,
            "keyboardstudio_one",
            Block(FirstId, "keyboardstudio_one"),
            null);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSM002");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Read_WhenMarkersAreMalformed_ReturnsDiagnostic()
    {
        var result = XkbManagedBlockEditor.Read(
            $"// BEGIN KeyboardStudio {FirstId}\n",
            FirstId);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSM001");
    }

    private static string Block(string id, string section, string extra = "") => $$"""
        // BEGIN KeyboardStudio {{id}}
        partial alphanumeric_keys
        xkb_symbols "{{section}}" {
        {{extra}}    include "keyboardstudio(ks_test)"
        };
        // END KeyboardStudio {{id}}
        """;
}
