using System.Reflection.PortableExecutable;
using System.Text;

namespace KeyboardStudio.Build;

internal static class PortableExecutableExportReader
{
    private const int ExportDirectorySize = 40;
    private const int MaximumExportNameLength = 4096;
    private const uint MaximumNamedExports = 65535;

    public static IReadOnlySet<string> ReadNames(PEReader peReader)
    {
        ArgumentNullException.ThrowIfNull(peReader);
        var exportDirectory = peReader.PEHeaders.PEHeader?.ExportTableDirectory ?? default;
        if (exportDirectory.RelativeVirtualAddress == 0 || exportDirectory.Size < ExportDirectorySize)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var directoryReader = peReader
            .GetSectionData(exportDirectory.RelativeVirtualAddress)
            .GetReader();
        if (directoryReader.RemainingBytes < ExportDirectorySize)
        {
            throw new BadImageFormatException("The PE export directory is truncated.");
        }

        directoryReader.ReadUInt32();
        directoryReader.ReadUInt32();
        directoryReader.ReadUInt16();
        directoryReader.ReadUInt16();
        directoryReader.ReadUInt32();
        directoryReader.ReadUInt32();
        directoryReader.ReadUInt32();
        var numberOfNames = directoryReader.ReadUInt32();
        directoryReader.ReadUInt32();
        var addressOfNames = directoryReader.ReadUInt32();
        directoryReader.ReadUInt32();

        if (numberOfNames > MaximumNamedExports)
        {
            throw new BadImageFormatException("The PE export name count is unreasonable.");
        }

        if (numberOfNames == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (addressOfNames == 0)
        {
            throw new BadImageFormatException("The PE export name table is missing.");
        }

        var namesReader = peReader.GetSectionData(unchecked((int)addressOfNames)).GetReader();
        var requiredNameTableBytes = checked((int)numberOfNames * sizeof(uint));
        if (namesReader.RemainingBytes < requiredNameTableBytes)
        {
            throw new BadImageFormatException("The PE export name table is truncated.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < numberOfNames; index++)
        {
            var nameRva = namesReader.ReadUInt32();
            if (nameRva == 0)
            {
                throw new BadImageFormatException("A PE export has an invalid name address.");
            }

            names.Add(ReadNullTerminatedUtf8(peReader, unchecked((int)nameRva)));
        }

        return names;
    }

    private static string ReadNullTerminatedUtf8(PEReader peReader, int relativeVirtualAddress)
    {
        var reader = peReader.GetSectionData(relativeVirtualAddress).GetReader();
        var bytes = new List<byte>();
        while (reader.RemainingBytes > 0 && bytes.Count < MaximumExportNameLength)
        {
            var value = reader.ReadByte();
            if (value == 0)
            {
                return Encoding.UTF8.GetString(bytes.ToArray());
            }

            bytes.Add(value);
        }

        throw new BadImageFormatException("A PE export name is not null terminated.");
    }
}
