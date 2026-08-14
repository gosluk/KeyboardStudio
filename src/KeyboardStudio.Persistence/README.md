# KeyboardStudio.Persistence

Project persistence implementation.

Initial responsibilities:

- implement `IKeyboardProjectStore`;
- serialize and deserialize `.kbdproj` JSON;
- preserve `schemaVersion`;
- validate/migrate persistence schemas before mapping them to the domain model.

Use `System.Text.Json` for the initial implementation.
