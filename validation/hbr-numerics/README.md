# HBR numeric formula regression

No Godot process, game assemblies, third-party packages, or network connection is required to run these checks. A .NET SDK supporting `net9.0` is required.

From the repository root:

```powershell
dotnet run --project validation/hbr-numerics/PureMathChecks.csproj --configuration Release
```

The project compiles the real numeric formula, skill-coefficient profile, and saturation helper by relative path. It runs formula boundary checks, coefficient conversion/equivalence checks, and 480 saved reference vectors, then writes `bin/Release/net9.0/report.json` with both production source hashes. Build outputs and the report are ignored by the root `.gitignore`.

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
