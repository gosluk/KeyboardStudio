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
    /// Creates a view model on the shipped seed and template set, using the validator this suite
    /// composes rather than the application's default one.
    /// </summary>
    public static MainWindowViewModel Create(IProjectInteractionService interactionService) =>
        new(new KeyboardTemplateProvider(),
            interactionService,
            CreateValidator(),
            new EmbeddedSeedProjectSource());

    /// <summary>
    /// Creates a view model whose import catalog is the one supplied, rather than whatever the
    /// test host happens to have installed.
    /// </summary>
    public static MainWindowViewModel WithImportCatalog(
        ILayoutImportCatalog catalog,
        IProjectInteractionService interactionService,
        IHostLayoutProbe? hostLayoutProbe = null) =>
        new(new KeyboardTemplateProvider(),
            interactionService,
            CreateValidator(),
            new EmbeddedSeedProjectSource(),
            catalog,
            hostLayoutProbe ?? new FakeHostLayoutProbe(null),
            new SilentLinuxUserVariantWorkflowService());

    private static KeyboardProjectValidator CreateValidator() =>
        new KeyboardProjectValidator([
            new MetadataValidationRule(),
            new PhysicalKeyboardValidationRule(),
            new MappingValidationRule()
        ]);
}
