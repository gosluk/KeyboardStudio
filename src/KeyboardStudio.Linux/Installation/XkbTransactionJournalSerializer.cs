using System.Text.Json;

namespace KeyboardStudio.Linux;

public static class XkbTransactionJournalSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Serialize(XkbTransactionJournal journal)
    {
        Validate(journal);
        return JsonSerializer.Serialize(journal, Options) + "\n";
    }

    public static XkbTransactionJournal Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            var journal = JsonSerializer.Deserialize<XkbTransactionJournal>(json, Options)
                ?? throw new InvalidDataException("The XKB transaction journal is empty.");
            Validate(journal);
            return journal;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The XKB transaction journal is malformed.", exception);
        }
    }

    private static void Validate(XkbTransactionJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        if (journal.SchemaVersion != XkbTransactionJournal.CurrentSchemaVersion ||
            journal.TransactionId.Length != 32 ||
            journal.TransactionId.Any(character => !char.IsAsciiHexDigit(character)) ||
            journal.ProjectInstallationId.Length != 32 ||
            journal.ProjectInstallationId.Any(character => !char.IsAsciiHexDigit(character)) ||
            !Path.IsPathFullyQualified(journal.UserXkbRoot) ||
            !Path.IsPathFullyQualified(journal.StateRoot))
        {
            throw new InvalidDataException("The XKB transaction journal identity or roots are invalid.");
        }

        ArgumentNullException.ThrowIfNull(journal.Files);
        foreach (var file in journal.Files)
        {
            ArgumentNullException.ThrowIfNull(file);
            if (!IsSafeRelativePath(file.RelativePath) ||
                (file.Existed ? !IsSha256(file.Sha256) : file.Sha256 is not null))
            {
                throw new InvalidDataException("The XKB transaction journal contains an invalid backup record.");
            }
        }

        if (journal.Files.Select(file => file.RelativePath)
            .Distinct(StringComparer.Ordinal).Count() != journal.Files.Count ||
            (journal.ManifestExisted ? !IsSha256(journal.ManifestSha256) : journal.ManifestSha256 is not null))
        {
            throw new InvalidDataException("The XKB transaction journal has duplicate files or invalid manifest state.");
        }
    }

    private static bool IsSafeRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value) ||
            value.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && segments.All(segment => segment is not "." and not "..");
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
