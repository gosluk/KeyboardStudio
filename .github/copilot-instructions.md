# KeyboardStudio Copilot Instructions

Follow the repository-wide AI rules in [`AGENTS.md`](../AGENTS.md).

In particular, never introduce more than one top-level C# type in a `.cs` file. Classes, records, record structs, structs, interfaces, enums, and delegates each require their own file. Nested helper types are permitted when intentionally scoped inside their owning type.
