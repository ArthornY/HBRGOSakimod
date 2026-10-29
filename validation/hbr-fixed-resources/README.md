# Fixed native HP/DP resource checks

These checks load the exact selected mod and native `sts2.dll`, install the production resource transpilers, and exercise native variable storage, upgrades, clones, formatting and representative effect methods. Audio, logging, engine allocation and command leaves are controlled captures; this is not a live game or rendered localization test.

Build the source without packaging, then build `Checks.csproj`, supplying `GodotSharpAssemblyPath` when the default NuGet path is unavailable. Run the resulting `Checks.dll` with the mod DLL path, its expected SHA-256, the dependency directory and the `GodotSharp.dll` path, matching the four-argument bootstrap used by `hbr-enemy-resources`. Native formatting dependencies resolve from `HBR_NATIVE_RUNTIME_DIRECTORY`, defaulting to the installed game directory `D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64`.

Fixed values retain their original units in raw `DynamicVar` data. The production patch replaces only audited consumption and display reads. Percentages, life steal, phase resets, save fields, ordinary skill damage, and percentage-derived event healing are deliberately excluded.

## Storage audit

The installed `sts2.dll` SHA-256 is `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`. The explicit model registry has 103 native methods containing candidate variable reads. Four methods also write a variable:

| Method | Raw write | Treatment |
| --- | --- | --- |
| `AbyssalBaths.OnImmerse` async body | `Damage.BaseValue = Damage.BaseValue + 1` | Preserve the exact `dup → reader → decimal addition → same variable setter` raw read; convert the separate max-HP command and `DamageVar` consumption. |
| `JungleMazeAdventure.CalculateVars` | Randomize `SoloGold` and `JoinForcesGold` | Gold is not a resource mapping. Initialization/mutation methods are not patched. |
| `WhisperingHollow.CalculateVars` | Randomize `Gold` | Gold is not a resource mapping. Initialization/mutation methods are not patched. |
| `TabletOfTruth.Decipher` async body | Set `DecipherMaxHpLoss` from native integer `GetDecipherCost()` | The stored integer remains raw. Resource reads are consumed by commands/options; no scaled value feeds the setter. |

The native `DynamicVar.BaseValue`, `IntValue`, `UpgradeValueBy`, `ResetToBase`, `Clone` and setters remain unchanged. Selected model `CalculateVars`, `OnUpgrade` and `AfterDowngraded` storage methods are excluded. Remaining field writes are async state/await bookkeeping, card selections and local effect amounts, not persistent raw resource data. The event regression runs the real first and second `OnImmerse` effects, checks damage `300 → 400`, and verifies raw `3 → 4 → 5`; its post-set option refresh is isolated after the actual native setter completes. Disabled-mode damage stays `3 → 4`.

## Descriptions and command boundaries

The formatting tests use exact English templates read from the installed PCK with the actual native `LocTable`, `LocString.GetFormattedText`, `LocManager.SmartFormat` and game-provided SmartFormat assembly. Locale filesystem loading, UI locale selection and rendering are not executed. Regen and Paper Cuts use their native smart-description `Amount` argument, while stacks and stack decay stay unscaled. Cook retains its native `MaxHp` argument. Terminal and Night Terrors encode their fixed amounts literally: only the exact descriptions' `LocString` instances are tagged; substitution skips BBCode attributes/colors/IDs and requires one exact standalone numeric match. Unknown or ambiguous templates retain their original text.

Actual native Offering, Burn, Burning Blood, Regen, Paper Cuts, Regal Pillow and Abyssal Baths methods execute with captured command leaves. The production max-HP-to-DP prefix consumes the already converted amount without scaling again. Terminal, Night Terrors, Cook and Knowledge Demon fixed command call sites are audited and installed, but their complete room/selection/monster hosts are not simulated. Knowledge Demon's `PonderMove` heals exactly `30 × player count` and is the only native monster fixed-heal row; Waterfall Giant damage-derived healing and Test Subject/Illusion phase recovery stay excluded.
