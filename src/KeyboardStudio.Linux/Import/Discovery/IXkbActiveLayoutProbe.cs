namespace KeyboardStudio.Linux;

/// <summary>
/// Reads which layout this host is configured to type with.
///
/// Behind an interface for the same reason <see cref="IXkbDataRootLocator"/> is: the precedence
/// order is the whole substance of the thing, and it has to be exercisable without a host that has
/// actually been configured that way.
/// </summary>
public interface IXkbActiveLayoutProbe
{
    /// <summary>
    /// The configured layout, or <see cref="XkbActiveLayout.Fallback"/> when nothing says.
    /// Never throws and never returns null: a host that answers nothing is the ordinary case.
    /// </summary>
    XkbActiveLayout Detect();
}
