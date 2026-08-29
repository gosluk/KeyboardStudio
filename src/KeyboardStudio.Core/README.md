# KeyboardStudio.Core

Platform-neutral domain and application core.

Responsibilities:

- `KeyboardProject` aggregate;
- physical keyboard and physical key models;
- versioned physical keyboard template contracts and `IKeyboardTemplateProvider`;
- keyboard layout and key mapping models;
- modifier/output abstractions;
- `KeyboardEditor` mutation service;
- composable metadata, physical-key, and mapping validation rules;
- stable Core diagnostic codes and severity-aware `ValidationResult`;
- the target-neutral layout import contract — `ILayoutImportSource`, `ILayoutImportCatalog`,
  and the descriptors, options, result, and `KSI` diagnostics they exchange;
- build/persistence abstractions shared by outer layers.

Layout import is defined here but implemented elsewhere. A layout is named only by opaque
source/layout/variant strings, so the parsers and tables that understand a platform's layout files
stay in that platform's assembly and Core acquires none of its vocabulary.

This project must not reference Avalonia, Windows APIs, WDK/MSVC types, or platform-specific UI services.
