namespace KeyboardStudio.Linux;

/// <summary>
/// The process environment, behind an interface so data-root ordering can be exercised without
/// mutating the variables of the test host.
/// </summary>
public interface IXkbEnvironment
{
    /// <summary>
    /// Returns the value of an environment variable, or <see langword="null"/> when it is unset.
    /// </summary>
    string? GetVariable(string name);
}
