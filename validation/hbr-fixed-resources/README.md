# Fixed native HP/DP resource checks

These checks load the exact selected mod and native `sts2.dll`, install the production resource transpilers, and exercise native variable storage, upgrades, clones, formatting and representative effect methods. Audio, logging, engine allocation and command leaves are controlled captures; this is not a live game or rendered localization test.

Build the source without packaging, then build `Checks.csproj`, supplying `GodotSharpAssemblyPath` when the default NuGet path is unavailable. Run the resulting `Checks.dll` with the mod DLL path, its expected SHA-256, the dependency directory and the `GodotSharp.dll` path, matching the four-argument bootstrap used by `hbr-enemy-resources`. Native formatting dependencies resolve from `HBR_NATIVE_RUNTIME_DIRECTORY`, defaulting to the installed game directory `D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64`.

Fixed values retain their original units in raw `DynamicVar` data. The production patch replaces only audited consumption and display reads. Percentages, life steal, phase resets, save fields, ordinary skill damage, and percentage-derived event healing are deliberately excluded.

The later effect-skill migration also excludes five status-card and four event `Damage` fields from fixed resource scaling. This fixture captures their raw skill-unit commands (`Burn=2`, successive `AbyssalBaths=3,4`) and verifies unchanged raw mutation; it does not resolve their damage at its isolated leaf. The [effect-skill fixture](../hbr-effect-skill-damage/README.md) exercises the separately declared calculation route where raw6 means100%. HP-loss costs, recovery and maximum-HP resources retain their existing conversion.

## Storage audit

The installed `sts2.dll` SHA-256 is `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`. The explicit model registry has 103 native methods containing candidate variable reads. Four methods also write a variable:

| Method | Raw write | Treatment |
| --- | --- | --- |
| `AbyssalBaths.OnImmerse` async body | `Damage.BaseValue = Damage.BaseValue + 1` | Preserve the exact raw setter and separate max-HP resource conversion. Damage now carries raw skill units through its explicitly declared source. |
| `JungleMazeAdventure.CalculateVars` | Randomize `SoloGold` and `JoinForcesGold` | Gold is not a resource mapping. Initialization/mutation methods are not patched. |
| `WhisperingHollow.CalculateVars` | Randomize `Gold` | Gold is not a resource mapping. Initialization/mutation methods are not patched. |
| `TabletOfTruth.Decipher` async body | Set `DecipherMaxHpLoss` from native integer `GetDecipherCost()` | The stored integer remains raw. Resource reads are consumed by commands/options; no scaled value feeds the setter. |

The native `DynamicVar.BaseValue`, `IntValue`, `UpgradeValueBy`, `ResetToBase`, `Clone` and setters remain unchanged. Selected model `CalculateVars`, `OnUpgrade` and `AfterDowngraded` storage methods are excluded. Remaining field writes are async state/await bookkeeping, card selections and local effect amounts, not persistent raw resource data. The event regression runs the real first and second `OnImmerse` effects and verifies raw `3 → 4 → 5`; its post-set option refresh is isolated after the actual native setter completes. Captured command inputs remain `3 → 4` in both modes; only the separate skill calculation interprets their units in HBR mode.

## Descriptions and command boundaries

The formatting tests use exact English templates read from the installed PCK with the actual native `LocTable`, `LocString.GetFormattedText`, `LocManager.SmartFormat` and game-provided SmartFormat assembly. Locale filesystem loading, UI locale selection and rendering are not executed. Regen and Paper Cuts use their native smart-description `Amount` argument, while stacks and stack decay stay unscaled. Cook retains its native `MaxHp` argument. Terminal and Night Terrors encode their fixed amounts literally: only the exact descriptions' `LocString` instances are tagged; substitution skips BBCode attributes/colors/IDs and requires one exact standalone numeric match. Unknown or ambiguous templates retain their original text.

Actual native Offering, Burn, Burning Blood, Regen, Paper Cuts, Regal Pillow and Abyssal Baths methods execute with captured command leaves. The production max-HP-to-DP prefix consumes the already converted amount without scaling again. Terminal, Night Terrors, Cook and Knowledge Demon fixed command call sites are audited and installed, but their complete room/selection/monster hosts are not simulated here. Knowledge Demon's `PonderMove` heals exactly `30 × player count`. Waterfall Giant's `SiphonMove` also heals a fixed `10/15 × player count`, independently of damage dealt, and now crosses the fixed-resource boundary. Regen resolves its actual declared async hook (`AfterSideTurnEnd` on stable, `BeforeSideTurnEndEarly` on beta). Test Subject/Illusion phase recovery stays excluded. The separate [enemy-healing fixture](../hbr-enemy-healing/README.md) covers those runtime-specific healing paths.

## Canonical templates and character selection

The 2026-10-29 game log showed character selection failing when formatting canonical `BurningBlood.DynamicVars.Heal`: the fixed-resource reader accessed `RelicModel.Owner`, which requires a mutable instance. The new regression reproduces the same `CanonicalModelException` against the installed pre-fix DLL (`D51979D401AAC9F08C752F2AABF5467779F9E1FBFD0EC810EFC62EF80EF519A2`). The earlier ownerless-variable check did not cover a variable attached to a canonical model.

The resource-owner reader now skips canonical templates before accessing any model's `Owner`. The Regen/Paper Cuts description postfix applies the same guard. Coverage includes real canonical Burning Blood variables; card, relic, potion and event templates; all four reader adapters; native string, highlighted and convertible formatting; native `LocManager`/SmartFormat with a minimal Heal fixture; both canonical power descriptions; mutable ownerless models; and owned instances with HBR enabled or disabled. Template reads retain native units, while owned HBR effects still convert exactly once.

Release source build and **127 checks passed**, with zero build warnings or errors. Tested DLL SHA-256: `BDC1852D9C4A3A016BBC2EBC12F7976F15D5D93B9EECC539D18652367F862029`. Source validation used `SkipModOutputCopy=true`; this checkout also needed `-p:DefaultExcludesInProjectFolder=outputs\**\*` to exclude C# generated by a previous packaging audit. No game UI, Godot process, export, packaging or installed mod replacement was performed.
