# Native BlockPotion scale regression

Run from this checkout with an explicit immutable candidate:

```powershell
./validation/block-potion-scale/Run.ps1 -ModAssemblyPath <candidate.dll> -Profile both
```

The script verifies the candidate SHA-256 and dependency-profile manifest hashes, builds only a managed validation host, and loads the same candidate on stable 0.107.1 and beta 0.113.1. It does not invoke Godot or rewrite native assemblies. Reports default to `outputs/hotaru-numeric-leaks-20261116/potion/final/{stable,beta}/report.json`.

The baseline `55A8A58C86D587FC3648BBD9217D3BFCC6B4CD9DB875424A42ADFEB7A63E511A` fails the first gameplay assertion on both profiles: real native `BlockPotion.OnUse` calls the typed `CreatureCmd.GainBlock` with its original raw12 `Unpowered` BlockVar, reaches the original decimal command, native `Hook.ModifyBlock`, and actual `Creature.GainBlockInternal`, then gains only12 at INT100 instead of1200. See the `potion/baseline` reports and captured native OnUse IL.

The final candidate `F66DA9F4A164D17751D9AC5528398784D3A1AE9A97C5ADA5E33E2B58056F23CC` passes59 assertions on each original profile, with both host builds at zero warnings/errors. The final reports record actual1200/1800 block, matching owned Chinese descriptions, legacy12, native hook/mutation counts and unchanged raw12. Beta emits the existing Sentry-without-Godot notice and exits successfully; no telemetry isolation or native assembly rewriting is used.

The focused regression keeps all those gameplay bodies and passively counts the decimal command, hook and mutation. Cases require1200 at INT100,1800 at INT150,12 with the numeric system disabled, preserved CardPlay/fast arguments, repeat grants without double scaling, unchanged raw12 storage, and unchanged behavior for different recipients, noncanonical cloned variables, unrelated relic variables and already-resolved decimal inputs. Existing Anchor and RippleBasin variables must retain their prior native-block conversion. Native `PotionModel.DynamicDescription`, LocManager and SmartFormat must show the same1200/1800/12 actually gained; unowned canonical templates remain native.

The shared stat fixture isolates StringName/logging and listener enumeration. CombatHistory.BlockGained recording and energy-icon pool/prefix presentation are additionally isolated; no block computation, block command or block mutation is replaced. The energy-icon leaf matches the existing Anchor fixture and avoids requiring every canonical character/potion pool in this minimal host; BlockPotion text contains no energy parameter. Localization loads existing native Chinese text read-only from `D:/stsmod/sts/localization/zhs`, merged with this checkout's localization. These are managed source/native-boundary checks; they do not load a user save, execute the complete potion inventory/consumption lifecycle, render a tooltip, run a live game, package, or change installed files.
