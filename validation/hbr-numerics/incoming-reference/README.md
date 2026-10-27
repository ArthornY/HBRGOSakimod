# Incoming-damage evidence snapshot

This folder preserves the source manifest, necessary formula excerpt, explicit transcription and comparison evidence. It is not official HBR source code and does not establish the exact final game damage calculation. Full acquisition copies are kept outside Git at `D:/stsmod/output/hbr-numeric-research-20261028/incoming-reference/`.

The primary source is [カンタータ仮面's own article](https://note.com/gazou774/n/n82b99b359f2b). `sources.json` records the article and exact image URLs, dates and acquisition SHA-256 values. The unchanged article HTML and images are archived outside Git:

- `cantata-incoming-formula.png`: information summary 2, dated **2025-08-03**. The incoming-damage box is at the lower right.
- `cantata-stat-weights.png`: information summary 4, dated **2026-04-29**. The defense rows are at the upper left.

Let `E` be enemy ability, `D` the selected weighted defense, `L/U` the skill's low/high power values, `T` the enemy threshold, and `R` the attack multiplier. The incoming box displays:

- When `D >= E`: `(L / T) * (E + T - D) * R`; the note says 1 damage once `D >= E + T`.
- When `D <= E`: `((U - L) / T * (E - D) + L) * R`; the note caps at the maximum once `D <= E - T`.

`cantata-incoming.cjs` is our clearly identified transcription of these displayed expressions at `R = 1`. It is an offline fixture oracle, not a file attributed to the author. The production `EvaluateIncomingAttackCurve` returns the same base curve with a 1 floor, no final rounding, no variance, and no multiplier application. The source does not settle the order of final minimum damage versus multipliers or exact rounding at those later stages.

The diagram says the threshold differs by enemy instance and can differ for the same enemy name in alternate dimensions or score attack. A fixed 140 is an STS adaptation value, not a verified universal HBR threshold.

The defense rows select `(2*VIT + SPR)/3` for normal defense and `(VIT + 2*SPR)/3` for special defense. They describe these as **mainly** single-target and **mainly** area attacks. The selected skill must determine the reference; the live target count and current HP/DP layer do not. Independent [AppMedia equipment-swap measurements](https://appmedia.jp/heavenburnsred/75256617) support this distinction and name the area attack Confusion Flash as a VIT-dependent exception. Those old measurements do not establish the present exact slopes or rounding.

## Independent implementation and the disagreement

`roy-incoming.ts` is the necessary unmodified source excerpt (lines 688-756) from [RoyWu0922/hbr_calc_web](https://github.com/RoyWu0922/hbr_calc_web), commit `68015b0b06699193c87963aa81e1204606661723`, `src/engine/damage.ts`. It includes the complete original `calcIncomingDamage` and its interfaces. The full original TS file and README are archived outside Git; their fixed commit, blob IDs and acquisition hashes remain in `sources.json`. The README says most of the project's code was AI-written and lists community spreadsheets/data as its sources.

For `x = E - D` in `-T < x < 0`, Roy uses `L + (L - 1) * x/T`. The primary diagram uses `max(1, L * (1 + x/T))`. These are different interpolations. For example, `L=1000, T=100, x=-50` gives **500** from the original diagram and **500.5** from Roy. Near the lower floor, `x=-99.95` gives **1** and **1.4995** respectively. The checked fixtures preserve and count the differences rather than widening the tolerance to hide them.

The source's other mechanics (enemy damage rate, accessory reductions and ±10% output ranges) remain visible in the snapshot but are outside the pure curve port. The later stat diagram's natural critical cap also differs from the already-frozen player calculator; this incoming task does not change the existing player damage/critical implementation.

## Reproduction and numerical guardrails

From the repository root:

```powershell
node validation/hbr-numerics/incoming-reference.cjs
dotnet run --project validation/hbr-numerics/PureMathChecks.csproj --configuration Release
```

Both commands work offline. The generator validates the two retained code-file hashes (`roy-incoming.ts` and `cantata-incoming.cjs`), executes only the extracted original Roy function (with its two TypeScript annotations removed), evaluates the primary-diagram transcription separately, and writes `incoming-vectors.json`. Its 104 cases include both thresholds, either side of each threshold, fractional skill powers, and both declared defense weight sets. **29** cases document the Roy disagreement. Primary comparisons use an absolute tolerance of `0.0000001` solely for JS floating-point versus C# decimal arithmetic, not a one-point formula tolerance.

`incoming-guardrail-vectors.json` separately covers 16 exact decimal extrema and malformed-input cases. These are implementation guardrails, not original HBR data or claims of agreement with JavaScript at decimal limits. Stats normalize to nonnegative, endpoints normalize to `1 <= L <= U`, and a nonpositive threshold produces a maximum/1 step without division by zero. Saturating intermediate arithmetic prevents decimal overflow. Increasing defensive stats is additionally checked for monotonic non-increasing base power.

The C# regression checks those two code hashes and reports the primary image date, compared commit, vector counts and documented disagreements. Original image/HTML/full-file hashes record acquisition checks only; the regression does not re-read or re-check the external research archive. Neither the oracle nor the regression invokes Godot or a game runtime.
