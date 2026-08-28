using KeyboardStudio.Core;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Builds view models for tests whose subject is unmapped-key behaviour.
/// </summary>
internal static class TestMainWindow
{
    /// <summary>
    /// Creates a view model whose new document is bare geometry rather than the shipped seed.
    /// </summary>
    public static MainWindowViewModel WithEmptyProject(
        IProjectInteractionService? interactionService = null,
        IKeyboardProjectValidator? validator = null) =>
        new(new KeyboardTemplateProvider(),
            interactionService ?? new SilentProjectInteractionService(),
            validator ?? CreateValidator(),
            new EmptySeedProjectSource());

    private static KeyboardProjectValidator CreateValidator() =>
        new KeyboardProjectValidator([
            new MetadataValidationRule(),
            new PhysicalKeyboardValidationRule(),
            new MappingValidationRule()
        ]);
}
