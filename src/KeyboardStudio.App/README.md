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

`ProjectDocumentService` owns New/Open/Save/Save As semantics, the current path, dirty state, and translation of expected persistence/I/O failures into presentation-safe errors. Avalonia storage pickers choose paths later; JSON serialization remains in `KeyboardStudio.Persistence`.

The main editor loads ISO-105 and ANSI-104 through `IKeyboardTemplateProvider`. Key ViewModels translate normalized template geometry into reference rendering dimensions, while the Avalonia `Canvas` and `Viewbox` position and uniformly scale the keyboard to the available surface. Key visuals remain simple buttons until P2.6 introduces the reusable `KeyControl`.

Must not contain Windows keyboard-table generation logic.
