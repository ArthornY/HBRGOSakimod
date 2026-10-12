---
name: hbrgo-packaging
description: Package HBRGOSakiMod or its SoundPack when the user requests a distributable. Not for source-only edits or validation.
---

# HBRGO packaging

## Scope and version

- Package the mod with distributable changes since its latest package: source, images, audio, localization, or manifest. Package both only if both changed or the user requests both. Instruction-file edits alone are not a reason to rebuild either mod.
- Do not independently rebuild, bump, or package an unchanged sibling mod. Dependency compilation required by the selected project is distinct from publishing the sibling; preserve the existing project-reference settings that suppress main-mod output copying.
- Follow the user's current packaging/version instruction. "版本号不变" means keep the manifest version unchanged. Otherwise, increment only the selected mod's manifest version by `0.0.0.1` once per requested package, not once per retry. Never bump a version for validation or rule maintenance.
- The user's standing preference is no compressed archive. A normal "打包" request delivers JSON, DLL, PCK, and PDB only. Create a ZIP only when the user explicitly requests one for that package.

## Resolve the selected project

Read only that project's `.csproj`, `export_presets.cfg`, manifest, and relevant build instructions when needed. Do not reconstruct the whole repository before a package build.

| Item | Main mod | SoundPack |
| --- | --- | --- |
| Project folder | Current main-mod Git checkout | Selected SoundPack project |
| Manifest source | `HBRGOSakiMod.json` in the project root | `build/HBRGOSakiMod_SoundPack.json` |
| Manifest delivered in `build/` | `HBRGOSakiMod.json` | `HBRGOSakiMod_SoundPack.json` |
| Artifact base name | `HBRGOSakiMod` | `HBRGOSakiMod_SoundPack` |
| Optional ZIP name, only when requested | `hbrgomod_v<version>.zip` | `hbrgomod_soundpack_v<version>.zip` |

Resolve the selected project from its `.csproj` and current Git checkout. Deliver to that project's `build/`; an explicit user destination takes precedence. A `test` checkout is a separate selected project: do not copy its experimental files into the main checkout or upload them unless the user explicitly requests that destination or upload.

The main manifest is source-controlled in the project root. Increment that source once when packaging, then deliver the matching copy in `build/`. `build/` contains generated files and is ignored by Git; it is not the source of the main version or dependency metadata. The main project's normal output-copy target also copies the root manifest. Source-only validation continues to use `SkipModOutputCopy=true`.

## Build and delivery

- Use Godot 4.5.1 Mono by default and the existing `Windows Desktop` export preset. Preserve resource exclusions and SoundPack audio inclusion rules; do not switch Godot versions simply because a newer executable is installed.
- Keep external runtime dependencies external. The main mod requires BaseLib and STS2-RitsuLib; SoundPack also requires the main mod. Preserve the main project's embedded Hextech compatibility build.
- A requested package is complete when the matching JSON, DLL, PCK, and PDB are delivered. Build success alone is not package completion. Validate the manifest version and delivered file hashes against the verified build; report the output directory, version, and validation result.
- If a ZIP is explicitly requested, also validate its four entries and manifest against the delivered files, and report its path and SHA-256. Use the manifest version in its filename, including when the user requests an unchanged version.
- Keep the last working package until its replacement has been built and verified. Leave existing archives alone unless the user asks to replace or remove them. When reusing older packaging scripts, remove their automatic ZIP creation, ZIP prerequisites, and archive cleanup from the default four-file workflow.
- In a separate worktree, sync and verify the four deliverables (plus a ZIP only when requested) at the canonical destination before removing temporary worktree `build/` staging. Resolve absolute paths and verify that staging is not the canonical output before cleanup. Never remove the canonical `build/` directory.
- Continue fixing failures caused by the requested changes and rerun the affected checks within the authorized local workflow. Report unrelated blockers and missing in-game validation; do not reset user changes, alter game saves, install mods into the game, or expand the build target merely to make validation pass.

## Maintenance

Keep this workflow task-scoped. Add new details only when they affect a real packaging decision; do not copy this body into always-loaded `AGENTS.md` files.

Guidance reviewed against [Rethinking skills and prompts for GPT-6 Astra](https://x.com/pvncher/status/2095991462416490862) on 2026-09-05; package paths and build behavior were checked locally.
