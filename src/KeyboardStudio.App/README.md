# KeyboardStudio.App

Avalonia presentation layer.

Responsibilities:

- application startup and dependency composition;
- `MainWindow` and editor views;
- `KeyboardEditorView`, `KeyMappingView`, and `BuildView`;
- ViewModels and UI commands;
- rendering keyboard templates as reusable key controls;
- application-level project document lifecycle through `IProjectDocumentService`;
- file-picker interaction for project Open/Save As paths;
- displaying validation, document, and build diagnostics;
- persisting host-local application preferences under `Settings/`;
- applying the saved appearance theme before the first window exists;
- owning the semantic colour contract every view draws through;
- presenting document and appearance commands in one application header.

`JsonApplicationSettingsStore` persists application preferences — currently the selected appearance theme — as versioned JSON under the per-user local application-data directory resolved by `IApplicationSettingsPathProvider`. These are host-local preferences, not portable project data, so they never enter `.kbdproj` documents or `KeyboardStudio.Persistence`. Reads are tolerant: a missing, unreadable, corrupt, unknown-theme, or future-schema file leaves the original file untouched and returns the Gray defaults with a presentation-safe error rather than failing startup. Writes go to a same-directory temporary file that is then moved into place, so an interrupted save cannot destroy the last complete settings file.

`ApplicationStartupSequence` is the composition order the application depends on: it loads the saved preferences, applies the theme they name, and only then builds the window. Restoring the appearance first is what stops the first frame rendering in the Fluent default and being corrected in front of the user, so the load is deliberately awaited rather than started alongside window construction. `AvaloniaApplicationThemeService` is the only type that touches `Application.RequestedThemeVariant`; everything above it, including ViewModels, deals in the neutral `ApplicationTheme`. `ApplicationThemeVariants` maps those three choices onto custom Avalonia variants that inherit Fluent Light (White, Gray) or Dark (Black), so a token KeyboardStudio does not define degrades to a readable Fluent colour instead of to nothing. The application no longer requests the `Default` variant and therefore no longer follows the operating-system theme.

`Styles/ThemeResources.axaml` holds the White, Gray, and Black palettes and is the only file in the application allowed to name a colour. Views and control templates reach it through `DynamicResource` alone, because Avalonia re-resolves only dynamic references when the variant changes — a `StaticResource` would keep whichever palette happened to be active when it was read. `ApplicationThemeTokens` is the required key set, and tests hold all three dictionaries to it, audit every view for a colour of its own, and check that every referenced key is one KeyboardStudio defines rather than one inherited from Fluent. `Styles/AppStyles.axaml` keeps structure and state, including the button theme whose primary, quiet, and destructive classes carry the action hierarchy; `Styles/IconResources.axaml` holds the application's own vector geometries.

`AppearanceViewModel` presents the three choices and commits the one the user picks. It applies the theme first and saves second: appearance is worth nothing if it takes a round-trip to disk to appear, and a preference that cannot be written is still a preference the user made, so a failed save keeps the chosen theme for the session and says so beside the choice instead of reverting the window or interrupting with a dialog. Choosing the theme that is already active does nothing, so reopening the menu does not rewrite the settings file. It holds no reference to `ProjectDocumentService`, and a regression test serializes a project either side of a theme change to prove appearance never becomes document data.

The application header carries the title, an icon File menu with every document command and its existing shortcut, the concise document label with the full path in a tooltip, the dirty badge, and the appearance menu. The standalone menu row is gone. Both icon triggers keep an accessible name and a tooltip, and the theme choices are a keyboard-navigable radio group with names and descriptions, so neither control depends on recognising its glyph.

`ProjectDocumentService` owns New/Open/Save/Save As semantics, the current path, dirty state, and translation of expected persistence/I/O failures into presentation-safe errors. `AvaloniaProjectInteractionService` supplies native storage pickers, unsaved-change confirmation, and error presentation while JSON serialization remains in `KeyboardStudio.Persistence`.

The main editor loads ISO-105 and ANSI-104 through `IKeyboardTemplateProvider`. Key ViewModels translate normalized template geometry into reference rendering dimensions, while the Avalonia `Canvas` and `Viewbox` position and uniformly scale the keyboard to the available surface. The reusable `KeyControl` renders the current output, physical/logical hint, and selected, unmapped, or error state. It forwards its click command to the editor ViewModel and contains no mapping mutation logic.

`KeyboardEditorViewModel` owns selection, active-layer presentation, logical-key choices, and the four layer mapping rows. Core mutations return whether state changed; only successful changes call the document dirty callback. This avoids observing the mutable domain graph and prevents rejected or no-op edits from marking a document dirty.

`DiagnosticsViewModel` presents the composed Core and Windows compatibility results ordered by severity. Key-associated error diagnostics drive `KeyControl` highlighting, and their commands navigate selection to the affected key. Successful mapping mutations rerun only the lightweight in-memory validation rules; native generation and compilation remain outside the edit path.

`IBuildTargetVisibilityPolicy` decides which build targets `BuildViewModel` offers, and is the only
place that decision is made. The shipped `EnvironmentBuildTargetVisibilityPolicy` offers Linux XKB
alone unless `KEYBOARDSTUDIO_TARGETS=all` is set. Hiding is presentation-only: every target keeps its
profile, `ExportTargetProfiles` keeps returning all of them, and the build orchestration below this
layer is unchanged.

`LinuxUserVariantViewModel` appears only for projects imported as new projects from the system XKB
catalog. It coordinates the Linux workflow service, presents capability and ownership states,
generates bundles beneath normal build output, and requires an exact-path confirmation before any
install, update, or uninstall. The chosen public variant ID and display name live in hidden Linux
target-profile settings so they round-trip with the project without becoming standalone build
fields. XKB translation, verification, XML merging, and filesystem transactions remain in
`KeyboardStudio.Linux`.

Must not contain Windows keyboard-table generation logic.
