using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Builds view models configured away from the shipping defaults, for tests whose subject is a
/// behaviour those defaults hide.
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

    /// <summary>
    /// Creates a view model that offers every build target, as a developer build started with
    /// <c>KEYBOARDSTUDIO_TARGETS=all</c> does. Tests that drive the Windows target through the UI
    /// need this: the shipped policy hides it.
    /// </summary>
    public static MainWindowViewModel WithAllBuildTargets(
        IProjectInteractionService interactionService) =>
        new(new KeyboardTemplateProvider(),
            interactionService,
            CreateValidator(),
            new EmbeddedSeedProjectSource(),
            new EnvironmentBuildTargetVisibilityPolicy(
                EnvironmentBuildTargetVisibilityPolicy.AllTargetsValue));

    /// <summary>
    /// Creates a view model whose import catalog is the one supplied, rather than whatever the
    /// test host happens to have installed.
    /// </summary>
    public static MainWindowViewModel WithImportCatalog(
        ILayoutImportCatalog catalog,
        IProjectInteractionService interactionService) =>
        new(new KeyboardTemplateProvider(),
            interactionService,
            CreateValidator(),
            new EmbeddedSeedProjectSource(),
            new EnvironmentBuildTargetVisibilityPolicy(
                EnvironmentBuildTargetVisibilityPolicy.AllTargetsValue),
            catalog);

    private static KeyboardProjectValidator CreateValidator() =>
        new KeyboardProjectValidator([
            new MetadataValidationRule(),
            new PhysicalKeyboardValidationRule(),
            new MappingValidationRule()
        ]);
}
