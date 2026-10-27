# HBR numeric formula regression

No Godot process, game assemblies, third-party packages, or network connection is required to run these checks. A .NET SDK supporting `net9.0` is required.

From the repository root:

```powershell
dotnet run --project validation/hbr-numerics/PureMathChecks.csproj --configuration Release
```

The project compiles the real numeric formula and saturation helper by relative path. It runs 38 boundary checks and 480 saved reference vectors, then writes `bin/Release/net9.0/report.json`. Build outputs and the report are ignored by the root `.gitignore`.

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
