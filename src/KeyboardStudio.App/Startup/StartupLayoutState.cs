namespace KeyboardStudio.App;

/// <summary>
/// What the editor is showing, as far as the startup layout is concerned.
/// </summary>
/// <remarks>
/// Distinct from the loader's own status: this is what happened to the document, which is not the
/// same question as what happened to the import. A layout can load perfectly and still be
/// discarded, because by then the user was working in something else.
/// </remarks>
public enum StartupLayoutState
{
    /// <summary>Startup loading has not been asked for.</summary>
    NotStarted,

    /// <summary>The current layout is being read.</summary>
    Loading,

    /// <summary>The editor is showing the layout this host types with.</summary>
    CurrentLayout,

    /// <summary>The editor is showing the built-in populated layout instead.</summary>
    SeedFallback,

    /// <summary>A layout arrived, but the user had already moved on.</summary>
    Discarded,
}
