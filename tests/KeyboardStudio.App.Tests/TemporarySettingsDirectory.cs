namespace KeyboardStudio.App.Tests;

/// <summary>
/// An isolated directory for settings tests.
/// </summary>
/// <remarks>
/// Application settings normally live in the developer's own local application-data directory.
/// Every settings test therefore runs against a directory created for that test alone, so a test
/// can neither read a preference the developer chose nor overwrite it.
/// </remarks>
internal sealed class TemporarySettingsDirectory : IDisposable
{
    public TemporarySettingsDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"KeyboardStudio-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) =>
        System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leaked temporary directory must never fail an otherwise passing test.
        }
    }
}
