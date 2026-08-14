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
        var path = CreateTemporaryImage(
            machine,
            isDll: true,
            PortableExecutableArtifactVerifier.RequiredExportName);
        try
        {
            var result = await CreateVerifier().VerifyAsync(path, target);

            Assert.True(result.Success);
            Assert.Equal(expectedMachine, result.Machine);
            Assert.True(result.IsDll);
            Assert.True(result.ExpectedExportFound);
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
        var path = CreateTemporaryImage(
            0xAA64,
            isDll: true,
            PortableExecutableArtifactVerifier.RequiredExportName);
        try
        {
            var result = await CreateVerifier().VerifyAsync(
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
        var path = CreateTemporaryImage(
            0x8664,
            isDll: false,
            PortableExecutableArtifactVerifier.RequiredExportName);
        try
        {
            var result = await CreateVerifier().VerifyAsync(
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

        var result = await CreateVerifier().VerifyAsync(
            path,
            BuildTarget.WindowsX64);

        Assert.False(result.Success);
        Assert.Contains(result.Messages, message => message.Code == "PE_FILE");
    }

    [Fact]
    public async Task VerifyAsync_WhenExpectedExportIsMissing_ReturnsDiagnostic()
    {
        var path = CreateTemporaryImage(0x8664, isDll: true, "DecoratedDescriptor");
        try
        {
            var result = await CreateVerifier().VerifyAsync(
                path,
                BuildTarget.WindowsX64);

            Assert.False(result.Success);
            Assert.False(result.ExpectedExportFound);
            var diagnostic = Assert.Single(result.Messages);
            Assert.Equal("PE_EXPORT", diagnostic.Code);
            Assert.Contains("KbdLayerDescriptor", diagnostic.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_WhenWindowsLoaderRejectsArtifact_ReturnsDiagnostic()
    {
        var path = CreateTemporaryImage(
            0x8664,
            isDll: true,
            PortableExecutableArtifactVerifier.RequiredExportName);
        try
        {
            var verifier = CreateVerifier(ArtifactLoadTestStatus.Failed);

            var result = await verifier.VerifyAsync(path, BuildTarget.WindowsX64);

            Assert.False(result.Success);
            Assert.Equal(ArtifactLoadTestStatus.Failed, result.LoadTest.Status);
            Assert.Contains(result.Messages, message => message.Code == "PE_LOAD");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static PortableExecutableArtifactVerifier CreateVerifier(
        ArtifactLoadTestStatus status = ArtifactLoadTestStatus.NotRun) =>
        new(new StaticLoadTester(status));

    private static string CreateTemporaryImage(
        ushort machine,
        bool isDll,
        string exportName)
    {
        const int peOffset = 0x80;
        const int coffHeaderOffset = peOffset + 4;
        const int optionalHeaderOffset = coffHeaderOffset + 20;
        const int sectionHeaderOffset = optionalHeaderOffset + 0xF0;
        const int sectionRawOffset = 0x200;
        const int sectionRva = 0x1000;
        var image = new byte[0x400];

        image[0] = (byte)'M';
        image[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(0x3c), peOffset);
        image[peOffset] = (byte)'P';
        image[peOffset + 1] = (byte)'E';
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(coffHeaderOffset), machine);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(coffHeaderOffset + 2), 1);
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
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeaderOffset + 56), 0x2000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeaderOffset + 60), 0x200);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(optionalHeaderOffset + 68), 3);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(optionalHeaderOffset + 72), 0x100000);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(optionalHeaderOffset + 80), 0x1000);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(optionalHeaderOffset + 88), 0x100000);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(optionalHeaderOffset + 96), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeaderOffset + 108), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeaderOffset + 112), sectionRva);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeaderOffset + 116), 0x100);

        ".rdata"u8.CopyTo(image.AsSpan(sectionHeaderOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(sectionHeaderOffset + 8), 0x200);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(sectionHeaderOffset + 12), sectionRva);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(sectionHeaderOffset + 16), 0x200);
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(sectionHeaderOffset + 20),
            sectionRawOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(sectionHeaderOffset + 36),
            0x40000040);

        var exportDirectory = image.AsSpan(sectionRawOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(exportDirectory[12..], sectionRva + 0x60);
        BinaryPrimitives.WriteUInt32LittleEndian(exportDirectory[16..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(exportDirectory[20..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(exportDirectory[24..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(exportDirectory[28..], sectionRva + 0x40);
        BinaryPrimitives.WriteUInt32LittleEndian(exportDirectory[32..], sectionRva + 0x44);
        BinaryPrimitives.WriteUInt32LittleEndian(exportDirectory[36..], sectionRva + 0x48);
        BinaryPrimitives.WriteUInt32LittleEndian(exportDirectory[0x40..], sectionRva + 0x100);
        BinaryPrimitives.WriteUInt32LittleEndian(exportDirectory[0x44..], sectionRva + 0x70);
        BinaryPrimitives.WriteUInt16LittleEndian(exportDirectory[0x48..], 0);
        "keyboard.dll\0"u8.CopyTo(exportDirectory[0x60..]);
        var exportNameBytes = System.Text.Encoding.ASCII.GetBytes(exportName + "\0");
        exportNameBytes.CopyTo(exportDirectory[0x70..]);

        var path = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path, image);
        return path;
    }

    private sealed class StaticLoadTester(ArtifactLoadTestStatus status) : IArtifactLoadTester
    {
        public Task<ArtifactLoadTestResult> TestAsync(
            string artifactPath,
            BuildTarget target,
            string exportName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArtifactLoadTestResult(status, "Load test result."));
    }
}
