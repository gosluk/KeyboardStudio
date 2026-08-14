# KeyboardStudio.App

Avalonia presentation layer.

Responsibilities:

- application startup and dependency composition;
- `MainWindow` and editor views;
- `KeyboardEditorView`, `KeyMappingView`, and `BuildView`;
- ViewModels and UI commands;
- rendering keyboard templates as reusable key controls;
- file-picker interaction for project save/load;
- displaying validation and build diagnostics.

Must not contain Windows keyboard-table generation logic.
