using System.Buffers.Binary;
using KeyboardStudio.Build;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class PortableExecutableArtifactVerifierTests
{
    [Theory]
    [InlineData(BuildTarget.WindowsX64, 0x8664, "Amd64")]
    [InlineData(BuildTarget.WindowsArm64, 0xAA64, "Arm64")]
    public async Task VerifyAsync_ForMatchingDll_ReturnsSuccess(
        BuildTarget target,
        ushort machine,
        string expectedMachine)
    {
        var path = CreateTemporaryImage(machine, isDll: true);
        try
        {
            var result = await new PortableExecutableArtifactVerifier().VerifyAsync(path, target);

            Assert.True(result.Success);
            Assert.Equal(expectedMachine, result.Machine);
            Assert.True(result.IsDll);
            Assert.Empty(result.Messages);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_ForWrongArchitecture_ReturnsDiagnostic()
    {
        var path = CreateTemporaryImage(0xAA64, isDll: true);
        try
        {
            var result = await new PortableExecutableArtifactVerifier().VerifyAsync(
                path,
                BuildTarget.WindowsX64);

            Assert.False(result.Success);
            Assert.Contains(result.Messages, message => message.Code == "PE_ARCH");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_ForImageWithoutDllFlag_ReturnsDiagnostic()
    {
        var path = CreateTemporaryImage(0x8664, isDll: false);
        try
        {
            var result = await new PortableExecutableArtifactVerifier().VerifyAsync(
                path,
                BuildTarget.WindowsX64);

            Assert.False(result.Success);
            Assert.Contains(result.Messages, message => message.Code == "PE_DLL");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_ForMissingFile_ReturnsDiagnostic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.dll");

        var result = await new PortableExecutableArtifactVerifier().VerifyAsync(
            path,
            BuildTarget.WindowsX64);

        Assert.False(result.Success);
        Assert.Contains(result.Messages, message => message.Code == "PE_FILE");
    }

    private static string CreateTemporaryImage(ushort machine, bool isDll)
    {
        const int peOffset = 0x80;
        const int coffHeaderOffset = peOffset + 4;
        const int optionalHeaderOffset = coffHeaderOffset + 20;
        var image = new byte[0x200];

        image[0] = (byte)'M';
        image[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(0x3c), peOffset);
        image[peOffset] = (byte)'P';
        image[peOffset + 1] = (byte)'E';
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(coffHeaderOffset), machine);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(coffHeaderOffset + 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(coffHeaderOffset + 16), 0xF0);
        var characteristics = (ushort)(0x0002 | 0x0020 | (isDll ? 0x2000 : 0));
        BinaryPrimitives.WriteUInt16LittleEndian(
            image.AsSpan(coffHeaderOffset + 18),
            characteristics);

        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(optionalHeaderOffset), 0x20B);
        BinaryPrimitives.WriteUInt64LittleEndian(
            image.AsSpan(optionalHeaderOffset + 24),
            0x0000000180000000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeaderOffset + 32), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeaderOffset + 36), 0x200);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(optionalHeaderOffset + 40), 6);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(optionalHeaderOffset + 48), 6);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeaderOffset + 56), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeaderOffset + 60), 0x200);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(optionalHeaderOffset + 68), 3);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(optionalHeaderOffset + 72), 0x100000);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(optionalHeaderOffset + 80), 0x1000);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(optionalHeaderOffset + 88), 0x100000);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(optionalHeaderOffset + 96), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeaderOffset + 108), 16);

        var path = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path, image);
        return path;
    }
}
