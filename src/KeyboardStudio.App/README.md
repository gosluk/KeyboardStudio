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

Must not contain Windows keyboard-table generation logic.
