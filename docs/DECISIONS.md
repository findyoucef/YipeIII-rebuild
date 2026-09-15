# Rebuild decisions

| ID | Decision | Reason and consequence |
| --- | --- | --- |
| D1 | macOS is the first supported platform; keep gameplay code portable to Windows. | The working machine is Apple Silicon macOS 26.6.2. Windows remains a planned second target and an important compatibility environment. |
| D2 | Use an adapted interface that preserves the original information, controls, assets, and gameplay timing where they affect behavior. | Native MFC cannot provide a maintainable cross-platform build. Visual differences must be recorded and checked against reference captures. |
| D3 | First implement a versioned native save format and an import-only legacy `.yp3` reader. | Original saves are useful evidence and should remain usable. Writing files for the original game will only be added if real-file tests show it can be done safely and losslessly. |
| D4 | C++20, CMake, SDL2, and the CTest test runner. | C++ expresses the recovered fixed-width behavior directly. SDL2 is available on macOS and Windows and covers windows, input, rendering, and audio. The core library has no SDL dependency. |
| D5 | Reconstruct all content present in supplied Yipe! III 1.1.6.0 (`FileVersion` and `ProductVersion` 1.1.6.0). | The PE timestamp is 2003-11-06. Missing registered-only content will be marked unavailable if it is not contained in this executable or reachable through supplied data. |
| D6 | Preserve observable quirks by default. | Each intentional correction goes in `UNKNOWNS.md` with its behavioral consequence and validation evidence. |

These choices can be revisited if reference testing contradicts an assumption. A revision must update this table, its affected tests, and the rebuild plan.
