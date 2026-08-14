using System.Globalization;
using System.Text;
using KeyboardStudio.Build;

namespace KeyboardStudio.Windows;

internal static class WindowsNativeSourceGenerator
{
    public static GeneratedSource Generate(WindowsKeyboardLayout layout, WindowsLayoutMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(metadata);

        return new GeneratedSource(new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["keyboard.c"] = WindowsCSourceGenerator.Generate(layout, metadata),
            ["keyboard.def"] = GenerateDefinitionFile(metadata),
            ["keyboard.h"] = GenerateHeader(),
            ["keyboard.rc"] = GenerateResourceScript(metadata)
        });
    }

    private static string GenerateDefinitionFile(WindowsLayoutMetadata metadata) =>
        $"LIBRARY {ToLibraryIdentifier(metadata.LayoutId)}\n\nEXPORTS\n    KbdLayerDescriptor @1\n";

    private static string GenerateHeader() =>
        "#pragma once\n\n#define KBD_TYPE 4\n#include <kbd.h>\n\nPKBDTABLES KbdLayerDescriptor(VOID);\n";

    private static string GenerateResourceScript(WindowsLayoutMetadata metadata)
    {
        var version = ParseVersion(metadata.FileVersion);
        var versionNumbers = string.Join(
            ",",
            version.Select(part => part.ToString(CultureInfo.InvariantCulture)));
        var versionString = string.Join(
            ".",
            version.Select(part => part.ToString(CultureInfo.InvariantCulture)));
        var fileName = $"{ToFileStem(metadata.LayoutId)}.dll";
        var builder = new StringBuilder();
        builder.AppendLine("#include <windows.h>");
        builder.AppendLine();
        builder.AppendLine("1 VERSIONINFO");
        builder.Append(" FILEVERSION ").AppendLine(versionNumbers);
        builder.Append(" PRODUCTVERSION ").AppendLine(versionNumbers);
        builder.AppendLine(" FILEFLAGSMASK 0x3fL");
        builder.AppendLine(" FILEFLAGS 0x0L");
        builder.AppendLine(" FILEOS VOS_NT_WINDOWS32");
        builder.AppendLine(" FILETYPE VFT_DLL");
        builder.AppendLine(" FILESUBTYPE VFT2_DRV_KEYBOARD");
        builder.AppendLine("BEGIN");
        builder.AppendLine("    BLOCK \"StringFileInfo\"");
        builder.AppendLine("    BEGIN");
        builder.AppendLine("        BLOCK \"040904B0\"");
        builder.AppendLine("        BEGIN");
        AppendResourceValue(builder, "CompanyName", metadata.CompanyName);
        AppendResourceValue(builder, "FileDescription", metadata.LayoutName);
        AppendResourceValue(builder, "FileVersion", versionString);
        AppendResourceValue(builder, "InternalName", metadata.LayoutId);
        AppendResourceValue(builder, "OriginalFilename", fileName);
        AppendResourceValue(builder, "ProductName", "KeyboardStudio Keyboard Layout");
        AppendResourceValue(builder, "ProductVersion", versionString);
        builder.AppendLine("        END");
        builder.AppendLine("    END");
        builder.AppendLine("    BLOCK \"VarFileInfo\"");
        builder.AppendLine("    BEGIN");
        builder.AppendLine("        VALUE \"Translation\", 0x0409, 1200");
        builder.AppendLine("    END");
        builder.AppendLine("END");
        return builder.ToString();
    }

    private static void AppendResourceValue(StringBuilder builder, string name, string value)
    {
        builder.Append("            VALUE \"")
            .Append(name)
            .Append("\", \"")
            .Append(EscapeResourceString(value))
            .AppendLine("\\0\"");
    }

    private static int[] ParseVersion(string value)
    {
        if (!Version.TryParse(value, out var version))
        {
            throw new ArgumentException("FileVersion must be a numeric version with two to four parts.", nameof(value));
        }

        var parts = new[]
        {
            version.Major,
            version.Minor,
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0)
        };
        if (parts.Any(part => part is < 0 or > ushort.MaxValue))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "FileVersion parts must fit in 16 bits.");
        }

        return parts;
    }

    private static string ToLibraryIdentifier(string value)
    {
        var identifier = string.Concat(value.Select(character =>
            char.IsAsciiLetterOrDigit(character) ? char.ToUpperInvariant(character) : '_'));
        if (identifier.Length == 0)
        {
            return "KEYBOARD";
        }

        return char.IsAsciiLetter(identifier[0]) ? identifier : $"KBD_{identifier}";
    }

    private static string ToFileStem(string value)
    {
        var fileStem = string.Concat(value.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
                ? char.ToLowerInvariant(character)
                : '_'));
        return fileStem.Length == 0 ? "keyboard" : fileStem;
    }

    private static string EscapeResourceString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
