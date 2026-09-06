using System.Diagnostics;
using System.Text.Json;

namespace KeyboardStudio.App;

public sealed class JsonApplicationSettingsStore : IApplicationSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IApplicationSettingsPathProvider _pathProvider;
    private readonly IApplicationSettingsFileSystem _fileSystem;

    public JsonApplicationSettingsStore(IApplicationSettingsPathProvider pathProvider)
        : this(pathProvider, new SystemApplicationSettingsFileSystem())
    {
    }

    internal JsonApplicationSettingsStore(
        IApplicationSettingsPathProvider pathProvider,
        IApplicationSettingsFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _pathProvider = pathProvider;
        _fileSystem = fileSystem;
    }

    public async Task<ApplicationSettingsLoadResult> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        string path;
        try
        {
            path = ResolvePath();
            if (!_fileSystem.FileExists(path))
            {
                return ApplicationSettingsLoadResult.Loaded(ApplicationSettings.Default);
            }

            var json = await _fileSystem.ReadAllTextAsync(path, cancellationToken);
            return Parse(json);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            return Default(ApplicationSettingsErrorKind.AccessDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return Default(ApplicationSettingsErrorKind.Io, exception.Message);
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return Default(ApplicationSettingsErrorKind.InvalidPath, exception.Message);
        }
    }

    public async Task<ApplicationSettingsSaveResult> SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.SchemaVersion != ApplicationSettings.CurrentSchemaVersion)
        {
            return Fail(
                ApplicationSettingsErrorKind.UnsupportedSchema,
                $"Settings schema version {settings.SchemaVersion} is not supported.");
        }

        if (!Enum.IsDefined(settings.Theme))
        {
            return Fail(
                ApplicationSettingsErrorKind.UnknownTheme,
                $"Application theme value '{settings.Theme}' is not supported.");
        }

        string? temporaryPath = null;
        try
        {
            var path = ResolvePath();
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                return Fail(
                    ApplicationSettingsErrorKind.InvalidPath,
                    $"The settings path '{path}' has no parent directory.");
            }

            _fileSystem.CreateDirectory(directory);

            temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            var json = Serialize(settings);
            await _fileSystem.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            _fileSystem.MoveFile(temporaryPath, path, overwrite: true);
            temporaryPath = null;
            return ApplicationSettingsSaveResult.Saved();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            return Fail(ApplicationSettingsErrorKind.AccessDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return Fail(ApplicationSettingsErrorKind.Io, exception.Message);
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return Fail(ApplicationSettingsErrorKind.InvalidPath, exception.Message);
        }
        finally
        {
            if (temporaryPath is not null)
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }
    }

    private static ApplicationSettingsLoadResult Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("schemaVersion", out var schemaElement) ||
                !schemaElement.TryGetInt32(out var schemaVersion))
            {
                return Default(
                    ApplicationSettingsErrorKind.InvalidData,
                    "The application settings document is missing a valid schemaVersion.");
            }

            if (schemaVersion != ApplicationSettings.CurrentSchemaVersion)
            {
                return Default(
                    ApplicationSettingsErrorKind.UnsupportedSchema,
                    $"Settings schema version {schemaVersion} is not supported.");
            }

            if (!document.RootElement.TryGetProperty("theme", out var themeElement) ||
                themeElement.ValueKind != JsonValueKind.String)
            {
                return Default(
                    ApplicationSettingsErrorKind.InvalidData,
                    "The application settings document is missing a valid theme.");
            }

            var themeIdentifier = themeElement.GetString();
            if (!TryParseTheme(themeIdentifier, out var theme))
            {
                return Default(
                    ApplicationSettingsErrorKind.UnknownTheme,
                    $"Application theme '{themeIdentifier}' is not supported.");
            }

            return ApplicationSettingsLoadResult.Loaded(new ApplicationSettings(schemaVersion, theme));
        }
        catch (JsonException exception)
        {
            return Default(ApplicationSettingsErrorKind.InvalidData, exception.Message);
        }
    }

    private static string Serialize(ApplicationSettings settings)
    {
        var document = new
        {
            schemaVersion = settings.SchemaVersion,
            theme = ToIdentifier(settings.Theme),
        };
        return JsonSerializer.Serialize(document, SerializerOptions) + Environment.NewLine;
    }

    private string ResolvePath()
    {
        var path = _pathProvider.GetSettingsPath();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path);
    }

    private static bool TryParseTheme(string? identifier, out ApplicationTheme theme)
    {
        theme = identifier switch
        {
            "white" => ApplicationTheme.White,
            "gray" => ApplicationTheme.Gray,
            "black" => ApplicationTheme.Black,
            _ => ApplicationTheme.Gray,
        };
        return identifier is "white" or "gray" or "black";
    }

    private static string ToIdentifier(ApplicationTheme theme) => theme switch
    {
        ApplicationTheme.White => "white",
        ApplicationTheme.Gray => "gray",
        ApplicationTheme.Black => "black",
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unknown application theme."),
    };

    private static bool IsPathException(Exception exception) =>
        exception is ArgumentException or NotSupportedException or PathTooLongException;

    private static ApplicationSettingsLoadResult Default(
        ApplicationSettingsErrorKind kind,
        string message)
    {
        Trace.TraceWarning("Application settings load failed: {0}", message);
        return ApplicationSettingsLoadResult.Defaulted(new ApplicationSettingsError(kind, message));
    }

    private static ApplicationSettingsSaveResult Fail(
        ApplicationSettingsErrorKind kind,
        string message)
    {
        Trace.TraceWarning("Application settings save failed: {0}", message);
        return ApplicationSettingsSaveResult.Failed(new ApplicationSettingsError(kind, message));
    }

    private void TryDeleteTemporaryFile(string path)
    {
        try
        {
            _fileSystem.DeleteFile(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Application settings temporary-file cleanup failed: {0}", exception.Message);
        }
    }
}
