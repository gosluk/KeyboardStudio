# KeyboardStudio.Core

Platform-neutral domain and application core.

Responsibilities:

- `KeyboardProject` aggregate;
- physical keyboard and physical key models;
- keyboard layout and key mapping models;
- modifier/output abstractions;
- `KeyboardEditor` mutation service;
- project validation contracts and rules;
- build/persistence abstractions shared by outer layers.

This project must not reference Avalonia, Windows APIs, WDK/MSVC types, or platform-specific UI services.
