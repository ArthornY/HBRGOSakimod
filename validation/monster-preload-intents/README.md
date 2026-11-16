# Monster preload intents

This narrow managed host executes native `MonsterModel.AssetPaths` and private
`GetIntents` on every concrete HBR move-table monster, both as its canonical
prototype and an actual mutable but unbound clone. It checks potential debuff
assets for AbyssKnockerKiller and IvoryScooper without fabricating a Creature or
relaxing the native throwing Creature getter. Repeated preloads must preserve
native binding/RNG/machine references and saved table cursors.

For those two monsters it also executes the affected real bound move at normal
and high difficulty, distinguishing HBR enemy, legacy enemy and allied phantom.
Attack value, hit count, target scope and actual calamity/continuous-damage
applications remain covered. The previous published DLL reproduces the original
unbound exception through the same real native asset methods.

The project links the existing roster/flavor fixture without changing it or
running its full suites. Its documented attack/animation/wait/history/preview UI
terminals are isolated; native intent/asset enumeration, binding, move and status
logic remain real. It does not run Godot or verify a rendered room transition.
The two profile builds currently have one inherited nullable warning in the old
roster fixture and zero errors, separate from the production SDK strict build.

Build stable then beta sequentially with `ModAssemblyPath`, `NativeAssemblyPath`,
`NativeRitsuAssemblyPath` and, for beta, `BetaNativeChecks=true`; outputs go under
the current stage's `preload/<profile>/host`. Run its `Checks.dll` with candidate,
candidate SHA256, original runtime profile, GodotSharp path and report path.
The bootstrap validates every original profile file hash and does not rewrite
telemetry or runtime assemblies. Optional `--negative` runs only the published
failure cases.

The active stage runner is
`outputs/magic-hover-elite-load-20261115/rebind-owned.ps1`.
