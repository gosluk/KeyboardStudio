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
- displaying validation, document, and build diagnostics.

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
