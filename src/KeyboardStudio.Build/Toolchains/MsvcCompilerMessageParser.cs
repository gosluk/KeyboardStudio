using System.Globalization;
using System.Text.RegularExpressions;

namespace KeyboardStudio.Build;

public static partial class MsvcCompilerMessageParser
{
    public static IReadOnlyList<CompilerMessage> Parse(params ProcessResult[] results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var messages = new List<CompilerMessage>();
        foreach (var line in EnumerateLines(results))
        {
            var match = SourceDiagnosticPattern().Match(line);
            if (match.Success)
            {
                messages.Add(new CompilerMessage(
                    match.Groups["code"].Value,
                    match.Groups["message"].Value.Trim(),
                    ParseSeverity(match.Groups["severity"].Value),
                    match.Groups["file"].Value.Trim(),
                    ParseNumber(match.Groups["line"].Value),
                    ParseNumber(match.Groups["column"].Value)));
                continue;
            }

            match = ToolDiagnosticPattern().Match(line);
            if (match.Success)
            {
                messages.Add(new CompilerMessage(
                    match.Groups["code"].Value,
                    match.Groups["message"].Value.Trim(),
                    ParseSeverity(match.Groups["severity"].Value)));
            }
        }

        return messages;
    }

    private static IEnumerable<string> EnumerateLines(IEnumerable<ProcessResult> results)
    {
        foreach (var result in results)
        {
            using var output = new StringReader(result.StandardOutput);
            while (output.ReadLine() is { } line)
            {
                yield return line;
            }

            using var error = new StringReader(result.StandardError);
            while (error.ReadLine() is { } line)
            {
                yield return line;
            }
        }
    }

    private static CompilerMessageSeverity ParseSeverity(string value) =>
        value.Equals("warning", StringComparison.OrdinalIgnoreCase)
            ? CompilerMessageSeverity.Warning
            : CompilerMessageSeverity.Error;

    private static int? ParseNumber(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    [GeneratedRegex(
        @"^(?<file>.+?)\((?<line>\d+)(?:,(?<column>\d+))?\)\s*:\s*(?<severity>warning|error|fatal error)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceDiagnosticPattern();

    [GeneratedRegex(
        @"^(?:LINK|RC|CVTRES)\s*:\s*(?<severity>warning|error|fatal error)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ToolDiagnosticPattern();
}
