# Dual-sword chase validation

`Checks.csproj` links the existing managed effect/stat fixture and runs only `DualSwordsChaseCases`.
It accepts a candidate mod DLL and a hash-checked stable/beta native profile through `Bootstrap.cs`.

The suite exercises the actual native `SpendResources`, resource validation, `OnPlayWrapper`, replay generation, nested `AutoPlay`, and final pile cleanup. It installs the production mode, chase payment, combo-hit, CardPlayContext and MoreThanOneTruth wrapper patches together. The attack probes inspect the real shared damage pipeline and native hit-count hook; mode-restricted cards retain their real card bodies. Only presentation, clock, repaint notifications, restricted-card draw and attack execution terminals are isolated.

Coverage includes:

- Alternating normal/chase attacks, manual nonattack reset, automatic attack/nonattack isolation, nested automatic plays, asynchronous replay and failure cleanup.
- Floor-half energy costs, native X capture, shared Swordheart substitution with and without Clarity, unchanged star handling through the existing quote.
- Form +3 hits and chase +1 hit, final 75% damage, Enhancement's 100% damage, unchanged upgraded Innate, removal of the old energy/block bonuses.
- Cannon command context with identical owner/card source, pet ownership, unpowered/self/precalculated damage and unrelated previews without initialized deck piles.
- Earlier capture-prefix lifetime with both mode-compatible and automatically switching restricted cards; this protects the replacement prefix's `Priority.Last` ordering.

The final 2026-11-14 run passed 143 checks on each profile. Evidence is under `outputs/dual-swords-combo-20261114/{stable,beta}/chase-report.json`, with the candidate SHA in each report. This is managed source/build validation, not a rendered game or multiplayer test. No Godot, packaging or publishing occurs.
