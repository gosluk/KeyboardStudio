namespace KeyboardStudio.Core;

/// <summary>
/// Names the layout the host is already configured to use, as a reference the catalog can import.
///
/// It is the counterpart to <see cref="ILayoutImportCatalog"/> for the one import nobody asks for:
/// the editor has to open onto something, and the layout the user already types with is a better
/// guess than anything the application could pick. Like the catalog, it hands back an opaque
/// <see cref="ImportableLayoutReference"/>, so the domain never learns what a platform calls its
/// layouts or where it records the choice.
/// </summary>
public interface IHostLayoutProbe
{
    /// <summary>
    /// The layout this host is configured to use, or <see langword="null"/> when it says nothing.
    ///
    /// A host that cannot be asked is not a failure: implementations return <see langword="null"/>
    /// rather than throwing, and the caller keeps whatever it was going to start from. The returned
    /// reference names a layout that may not exist — configuration outlives the packages it names —
    /// so the import it feeds still has to be allowed to fail.
    /// </summary>
    ImportableLayoutReference? Detect();
}
