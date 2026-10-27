# HBR numeric formula regression

No Godot process, game assemblies, third-party packages, or network connection is required to run these checks. A .NET SDK supporting `net9.0` is required.

From the repository root:

```powershell
dotnet run --project validation/hbr-numerics/PureMathChecks.csproj --configuration Release
```

The project compiles the real numeric formula, skill-coefficient profile, and saturation helper by relative path. It runs formula boundary checks, coefficient conversion/equivalence checks, 480 saved player-formula reference vectors, and independent incoming-damage checks, then writes `bin/Release/net9.0/report.json` with both production source hashes and incoming source/coverage details. Build outputs and the report are ignored by the root `.gitignore`.

The coefficient profile is an adaptation policy: the old six-point strike is damage 100%, while the old five-point defend is block 100%. The damage profile has power 6000–12000 and parameter limit 140 at 100%. Block 100% has base power 500 before its intelligence support curve. For repeating percentages, configuration passes a percentage/divisor pair, such as `DamageUnits(50, 3)` for one native damage unit. Display percentages are never fed back into combat calculations. Multi-hit coefficients are checked per hit; actual HP/DP ratios and already-resolved resource values are outside this conversion.

The vectors come from the community calculator author's current implementation, not an official HBR formula publication:

- Repository: <https://github.com/hbr-tool/damage-calculator>
- Frozen commit: `ead9f319d18fb47e749718adac0d4384b785c0bc`
- Functions: `calcAttackEffectSize`, `calcBuffEffectSize`, `calcDebuffEffectSize` in `src/components/dmgCalc/logic.js`.
- Raw source SHA-256: `0e60cc885d9e48773dc36254116a8526912bed2f068c5973fe3505175a651f0c`.
- Attack comparisons allow a 0.01 difference because the author uses binary floating point followed by hundredths truncation, while the port uses decimal. Buff/debuff tolerance is 0.000001.

To deliberately regenerate the checked-in vectors, use Node.js 18 or later. This optional step downloads only the pinned source, verifies its hash, executes the three named pure functions, and replaces `reference-vectors.json`:

```powershell
node validation/hbr-numerics/reference.cjs
```

Review regenerated vectors before committing them. Source download failures, hash mismatches, or missing functions fail without replacing the existing vectors. No branch-head URL or latest-version dependency is used.

## Incoming damage

The incoming curve has a separate primary source: [カンタータ仮面's original research diagrams](https://note.com/gazou774/n/n82b99b359f2b), incoming formula dated 2025-08-03 and defense weights dated 2026-04-29. It is a community reconstruction, not official exact game code. Enemy ability and the skill's selected weighted VIT/SPR defense enter a capped incoming curve; the existing player outgoing curve and its overflow behavior remain unchanged. Enemy thresholds depend on encounter variants, so the STS adaptation threshold 140 must not be described as an original universal value.

[The source manifest and necessary excerpt](incoming-reference/README.md) record exact URLs/commits/blob IDs, acquisition hashes, formulas and limits. Full research snapshots remain outside Git in `D:/stsmod/output/hbr-numeric-research-20261028/incoming-reference/`; each offline run checks only the two retained code hashes. The independent RoyWu implementation disagrees with the original diagram's negative slope; 104 saved cases include **29 explicitly documented differences**. Comparisons follow the original diagram rather than hiding these differences in a tolerance. Another 16 exact decimal fixtures cover extreme and malformed inputs. These checks validate the pure base curve; they do not validate game data mappings, final rounding, multiplier order, UI or gameplay.

Regenerate only the incoming fixture offline with:

```powershell
node validation/hbr-numerics/incoming-reference.cjs
```
