# Project build constraints

- Keep both BaseLib and STS2-RitsuLib as runtime dependencies for future development and builds.
- The default editor/export runtime is Godot 4.5.1 Mono.
- Do not invoke Godot, export a PCK, or create a distributable package until the user explicitly asks to package ("打包"). Source-only validation with `dotnet build` is allowed.
- On every requested package build, increment the mod version in `build/HBRGOSakiMod.json` by `0.0.0.1` before packaging.
- Every package build must deliver the current JSON, DLL, PCK, PDB, and ZIP artifacts to the canonical project folder `/Users/arthorn/Documents/GitHub/HBRGOSakimod/build/`: `HBRGOSakiMod.json`, `HBRGOSakiMod.dll`, `HBRGOSakiMod.pck`, `HBRGOSakiMod.pdb`, and `hbrgomod_v<version>.zip` (for example, `hbrgomod_v1.0.3.2.zip`). Before delivering a new package, delete every older `hbrgomod*.zip` so that only the ZIP for the current version remains. A Codex worktree may use its local `build/` only as temporary packaging staging; after the five artifacts are synced and verified, delete the entire worktree `build/` directory so no Codex-local build copy is retained.
- In future localization text, represent gained or lost energy with the same number of vanilla-style energy icons (for example, BaseLib SimpleLoc `[E]` and `[EE]`) instead of writing the amount as plain text.
