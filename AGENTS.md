# KeyboardStudio AI Rules

These rules apply to all AI-assisted changes in this repository.

## C# type organization

- Every top-level C# type declaration must live in its own `.cs` file.
- This includes classes, records, record structs, structs, interfaces, enums, and delegates, regardless of accessibility.
- Do not place multiple top-level types in one source file, even when they are small or tightly related.
- Nested helper types are allowed when they are intentionally scoped inside their owning type; they are not top-level types.
- Keep a type in the folder that represents its architectural subsystem and use a descriptive file name matching the type where practical. Avalonia/XAML code-behind naming remains conventional (for example, `MainWindow.axaml.cs`).
- When modifying an existing multi-type file, split all of its top-level types rather than adding another exception.
- Before completing a change, audit all touched C# files for compliance with this rule.
