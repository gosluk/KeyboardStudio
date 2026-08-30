using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KeyboardStudio.Linux;

/// <summary>Edits only comment-delimited KeyboardStudio blocks in a shared symbols file.</summary>
public static partial class XkbManagedBlockEditor
{
    public static XkbManagedBlockEditResult Upsert(
        string existingContent,
        string projectInstallationId,
        string publicVariantId,
        string desiredBlock,
        string? expectedExistingBlockSha256)
    {
        ArgumentNullException.ThrowIfNull(existingContent);
        ValidateId(projectInstallationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicVariantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(desiredBlock);

        var diagnostics = new List<XkbDiagnostic>();
        var blocks = Parse(existingContent, diagnostics);
        var desired = Parse(NormalizeBlock(desiredBlock), diagnostics);
        var desiredTarget = desired.Count == 1 &&
                            string.Equals(desired[0].Id, projectInstallationId, StringComparison.Ordinal)
            ? desired[0]
            : null;
        if (desiredTarget is null)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSM001",
                "The proposed managed block does not contain exactly one matching ownership pair."));
        }

        if (diagnostics.Count > 0)
        {
            return Failed(existingContent, diagnostics);
        }

        var target = blocks.SingleOrDefault(block =>
            string.Equals(block.Id, projectInstallationId, StringComparison.Ordinal));
        var contentOutsideTarget = target is null
            ? existingContent
            : existingContent.Remove(target.Start, target.Length);
        if (SymbolsSectionPattern(publicVariantId).IsMatch(contentOutsideTarget))
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSM002",
                $"XKB section '{publicVariantId}' already exists outside this project's managed block."));
            return Failed(existingContent, diagnostics);
        }

        if (target is null)
        {
            if (expectedExistingBlockSha256 is not null)
            {
                diagnostics.Add(new XkbDiagnostic(
                    "KSM003",
                    "The installation manifest expects a managed block that is missing."));
                return Failed(existingContent, diagnostics);
            }

            var block = NormalizeBlock(desiredBlock);
            var separator = existingContent.Length == 0
                ? string.Empty
                : existingContent.EndsWith("\n\n", StringComparison.Ordinal)
                    ? string.Empty
                    : existingContent.EndsWith('\n')
                        ? "\n"
                        : "\n\n";
            return Succeeded(existingContent + separator + block, block, changed: true);
        }

        var existingBlock = NormalizeBlock(existingContent.Substring(target.Start, target.Length));
        if (expectedExistingBlockSha256 is null ||
            !string.Equals(Hash(existingBlock), expectedExistingBlockSha256, StringComparison.Ordinal))
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSM003",
                "The existing managed block was changed outside KeyboardStudio."));
            return Failed(existingContent, diagnostics);
        }

        var replacement = NormalizeBlock(desiredBlock);
        var updated = existingContent.Remove(target.Start, target.Length)
            .Insert(target.Start, replacement);
        return Succeeded(updated, replacement, !string.Equals(updated, existingContent, StringComparison.Ordinal));
    }

    public static XkbManagedBlockEditResult Remove(
        string existingContent,
        string projectInstallationId,
        string expectedExistingBlockSha256)
    {
        ArgumentNullException.ThrowIfNull(existingContent);
        ValidateId(projectInstallationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedExistingBlockSha256);

        var diagnostics = new List<XkbDiagnostic>();
        var blocks = Parse(existingContent, diagnostics);
        if (diagnostics.Count > 0)
        {
            return Failed(existingContent, diagnostics);
        }

        var target = blocks.SingleOrDefault(block =>
            string.Equals(block.Id, projectInstallationId, StringComparison.Ordinal));
        if (target is null)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSM003",
                "The managed block recorded by the installation manifest is missing."));
            return Failed(existingContent, diagnostics);
        }

        var block = NormalizeBlock(existingContent.Substring(target.Start, target.Length));
        if (!string.Equals(Hash(block), expectedExistingBlockSha256, StringComparison.Ordinal))
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSM003",
                "The existing managed block was changed outside KeyboardStudio."));
            return Failed(existingContent, diagnostics);
        }

        var start = target.Start;
        var length = target.Length;
        if (start > 0 && existingContent[start - 1] == '\n')
        {
            start--;
            length++;
        }

        var updated = existingContent.Remove(start, length);
        return new XkbManagedBlockEditResult(
            true,
            string.IsNullOrWhiteSpace(updated) ? null : updated,
            null,
            Changed: true,
            []);
    }

    public static XkbManagedBlockEditResult Read(
        string content,
        string projectInstallationId)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateId(projectInstallationId);

        var diagnostics = new List<XkbDiagnostic>();
        var blocks = Parse(content, diagnostics);
        var target = blocks.SingleOrDefault(block =>
            string.Equals(block.Id, projectInstallationId, StringComparison.Ordinal));
        if (diagnostics.Count > 0 || target is null)
        {
            if (target is null)
            {
                diagnostics.Add(new XkbDiagnostic("KSM003", "The managed block is missing."));
            }

            return Failed(content, diagnostics);
        }

        var block = NormalizeBlock(content.Substring(target.Start, target.Length));
        return Succeeded(block, block, changed: false);
    }

    private static List<BlockRange> Parse(string content, List<XkbDiagnostic> diagnostics)
    {
        var starts = BeginPattern().Matches(content);
        var ends = EndPattern().Matches(content);
        if (starts.Count != ends.Count)
        {
            diagnostics.Add(new XkbDiagnostic("KSM001", "Managed block markers are unbalanced."));
            return [];
        }

        var blocks = new List<BlockRange>(starts.Count);
        for (var index = 0; index < starts.Count; index++)
        {
            var start = starts[index];
            var end = ends[index];
            if (end.Index < start.Index ||
                !string.Equals(start.Groups[1].Value, end.Groups[1].Value, StringComparison.Ordinal))
            {
                diagnostics.Add(new XkbDiagnostic("KSM001", "Managed block markers are nested or mismatched."));
                return [];
            }

            var endIndex = end.Index + end.Length;
            if (endIndex < content.Length && content[endIndex] == '\r')
            {
                endIndex++;
            }

            if (endIndex < content.Length && content[endIndex] == '\n')
            {
                endIndex++;
            }

            blocks.Add(new BlockRange(start.Groups[1].Value, start.Index, endIndex - start.Index));
        }

        if (blocks.Select(block => block.Id).Distinct(StringComparer.Ordinal).Count() != blocks.Count)
        {
            diagnostics.Add(new XkbDiagnostic("KSM001", "A managed project ID occurs more than once."));
        }

        return blocks;
    }

    private static Regex SymbolsSectionPattern(string section) =>
        new(
            $"xkb_symbols\\s+\"{Regex.Escape(section)}\"",
            RegexOptions.CultureInvariant);

    private static string NormalizeBlock(string block) =>
        block.Replace("\r\n", "\n", StringComparison.Ordinal).Trim() + "\n";

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static XkbManagedBlockEditResult Succeeded(
        string content,
        string block,
        bool changed) =>
        new(true, content, Hash(block), changed, []);

    private static XkbManagedBlockEditResult Failed(
        string content,
        IReadOnlyList<XkbDiagnostic> diagnostics) =>
        new(false, content, null, Changed: false, diagnostics);

    private static void ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Length != 32 || id.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("A managed block ID must be 32 hexadecimal digits.", nameof(id));
        }
    }

    [GeneratedRegex(
        @"(?m)^// BEGIN KeyboardStudio ([0-9A-Fa-f]{32})\r?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex BeginPattern();

    [GeneratedRegex(
        @"(?m)^// END KeyboardStudio ([0-9A-Fa-f]{32})\r?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex EndPattern();

    private sealed record BlockRange(string Id, int Start, int Length);
}
