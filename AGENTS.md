# Main mod guidance

- Keep BaseLib and STS2-RitsuLib as runtime dependencies. The default editor/export runtime is Godot 4.5.1 Mono.
- Do not invoke Godot, export a PCK, or create a distributable until the user explicitly requests packaging ("打包"). Source-only `dotnet build` is allowed; use `-p:SkipModOutputCopy=true` when validation should leave delivered DLL/PDB files untouched.
- For packaging, read [.agents/skills/hbrgo-packaging/SKILL.md](.agents/skills/hbrgo-packaging/SKILL.md). Follow the user's current version instruction, including "版本号不变", and leave an unchanged SoundPack alone.
- Read and validate only the areas affected by the task. Continue through implementation and appropriate checks without stopping merely at a first draft; distinguish source/build validation from an actual in-game test.
- In localization, show gained/lost energy with the matching number of vanilla-style energy icons, such as BaseLib SimpleLoc `[E]` and `[EE]`, instead of plain numeric energy text.
- Default new card portraits to `images/packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png` unless the user provides another image.
