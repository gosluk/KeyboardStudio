# KeyboardStudio.Windows

Windows keyboard-layout translation and native source generation.

Responsibilities:

- translate `KeyboardProject` into an internal Windows keyboard model;
- map scan codes to Windows virtual keys;
- translate generic modifier layers to Windows modifier states;
- validate Windows-only logical-key and modifier compatibility behind a Core rule contract;
- publish stable `KSW` compatibility diagnostic codes;
- generate deterministic native keyboard-layout source;
- isolate all Windows keyboard-table knowledge from the Avalonia UI and core domain.

This project generates source; it does not own compiler process execution.
