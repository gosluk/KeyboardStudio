namespace KeyboardStudio.Core;

/// <summary>
/// Raised when a seed project cannot be produced. A failure here is a packaging defect,
/// not user error: seeds ship with the application.
/// </summary>
public sealed class SeedProjectException : Exception
{
    public SeedProjectException(string seedId, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        SeedId = seedId;
    }

    /// <summary>
    /// Identifier of the seed that could not be produced.
    /// </summary>
    public string SeedId { get; }
}
