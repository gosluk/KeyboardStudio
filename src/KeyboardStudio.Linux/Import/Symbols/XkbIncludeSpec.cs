namespace KeyboardStudio.Linux;

/// <summary>
/// One file-and-section pair named by an include, with the rule for combining it.
///
/// A single include string can name several: <c>"us(basic)+de(nodeadkeys)"</c> is two of these, and
/// the separator carries meaning — <c>+</c> composes with <see cref="XkbMergeMode.Override"/> and
/// <c>|</c> with <see cref="XkbMergeMode.Augment"/> — so the separators are read as merge operators
/// rather than punctuation.
/// </summary>
/// <param name="File">
/// The symbols file name relative to a <c>symbols/</c> directory. It may name a subdirectory, as
/// <c>sun_vndr/us</c> does.
/// </param>
/// <param name="Section">
/// The section within that file, or <see langword="null"/> for the file's default section, which is
/// what a bare <c>include "us"</c> means.
/// </param>
/// <param name="Merge">How this piece combines with what precedes it.</param>
/// <param name="Group">
/// The keyboard group the include targets, from the <c>:2</c> suffix, defaulting to 1. The model
/// holds one group, so anything above 1 is dropped with <c>KSI020</c> rather than being flattened
/// into group 1, which would silently overwrite the layout the user actually asked for.
/// </param>
public sealed record XkbIncludeSpec(string File, string? Section, XkbMergeMode Merge, int Group = 1);
