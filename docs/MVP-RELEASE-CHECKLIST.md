# KeyboardStudio MVP release checklist

This checklist is the evidence map for Phase 12. A release candidate is eligible only when the
GitHub Actions **MVP release gate** passes for its commit and the manual desktop observations below
have no unresolved defect.

## Automated evidence

| Exit criterion | Evidence |
| --- | --- |
| App starts on x64 Windows and Linux | Self-contained package jobs run `--version`, start the real Avalonia desktop process for five seconds, and archive the same directory. |
| ISO-105 and ANSI-104 render correctly | Core template geometry tests and application geometry ViewModel tests. |
| Four modifier layers can be edited | Core editor tests plus `MvpEndToEndScenarioTests`, including Default, Shift, AltGr, and Shift+AltGr. |
| Save/load and target profiles are reliable | The end-to-end scenario saves the document envelope, reopens it, and checks both target profiles and Unicode mappings. Legacy direct schema-v1 files remain covered. |
| Invalid projects produce actionable diagnostics | The `ErrorPath` facet covers malformed/future files, missing profiles, target compatibility, toolchain/compiler/verifier failures, blocked output, and cancellation. |
| A valid project produces real Windows `KBDTABLES` source | Windows golden/source tests cover generated C, header, DEF, and resources. |
| Windows source compiles to a DLL | `WindowsIntegration` compiles representative ANSI, AltGr, ISO, and extended-key fixtures with MSVC x64. |
| The DLL passes verification | Windows integration requires x64 PE/DLL flags, exact `KbdLayerDescriptor` export, load resolution, and reproducible output. |
| The same project produces deterministic XKB v1 symbols | Linux unit/golden tests and the application end-to-end scenario verify stable symbols and manifests. |
| XKB passes `xkbcli` on Linux CI | `XkbIntegration` compiles representative ISO and ANSI output with the required external verifier. |
| Linux and Windows integration CI are green | The final gate depends on managed, XKB, Windows-native, and both packaging jobs. |
| Documentation matches behavior | README and project-format, architecture, Windows, Linux, packaging, versioning, diagnostics, and testing guides describe the shipped workflow. |

## Manual release observations

Automation proves startup and the artifact pipeline but cannot judge visual usability. Before
turning a release candidate into downloadable release assets:

- open the packaged application on a Windows 10/11 x64 desktop and one supported x64 Linux desktop;
- inspect ISO-105 and ANSI-104 geometry at normal and high-DPI scaling;
- edit all four layers, save, close, reopen, and confirm the selected profile values;
- generate one Windows DLL and one Linux symbols component from the same project;
- confirm failure messages remain readable at the packaged window size;
- verify the archives contain no user project, generated layout, signing secret, or retained build
  workspace.

Record the tested OS versions and any waived limitation in the release notes. Windows installation,
Linux desktop registration/activation, signing, and installers remain outside the MVP and must not
be represented as completed by this checklist.
