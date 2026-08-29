namespace KeyboardStudio.App;

/// <summary>
/// What an accepted import does to the document that is already open.
/// </summary>
public enum LayoutImportCommitMode
{
    /// <summary>
    /// Replaces the document with the imported one: new geometry, new mappings, default build
    /// settings, and no file path until it is saved.
    /// </summary>
    NewProject = 0,

    /// <summary>
    /// Keeps the open document — its geometry, its build settings, and the file it is saved as —
    /// and replaces only what the keys produce. This is how a layout is used as a starting point
    /// for work already under way.
    /// </summary>
    ReplaceMappings = 1
}
