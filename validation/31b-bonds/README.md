# 31B bonds native behavior checks

This suite validates requirements 38, 39 and 43 against the actual compiled mod and native STS2 assemblies. It covers marker acquisition through `TeamCollectionRelics.EnsureObtained` and `RelicCmd.Obtain`, food receipts and one deterministic recipient selection per new food, native saved-property round trips, additive per-hit base coefficients and selected-target/actual damage equality, protective charms with native Artifact and negative Strength, live Vitality/Spirit, six-member replenishment, and real draw/discard-to-hand commands with separate ordinary/enemy/personal-extra-turn budgets.

The fixture's food pickup relic is an inert native `RelicModel` whose ID is set to each whitelisted food. This executes the real Obtain receipt while keeping unrelated vanilla food pickup effects outside the test. Actor state and inserted-turn admission are controlled setup; visual/history/terminal health-write leaves are isolated. The suite does not run Godot, UI rendering, live multiplayer transport, packaging or uploading.

Build `Checks.csproj` with `ModAssemblyPath`, `NativeAssemblyPath`, `NativeRitsuAssemblyPath`, `OutputPath`, and `BetaNativeChecks=true` for beta. Run `Checks.dll` with five positional arguments: immutable candidate DLL, expected SHA256, native profile directory, GodotSharp DLL, and report path. The bootstrap verifies candidate and profile hashes before loading either assembly. Stable and beta need matching compile/runtime profiles; beta saved-property cache setup uses the real new native registration API.

Latest integration evidence is recorded under `outputs/31b-monsters-20261114/{stable,beta}/bond-report.json`. Each report includes its bound candidate SHA and native profile. Fixture compilation reuses older validation sources and currently emits their 23 nullable warnings; this is separate from the production mod's clean builds.

The closing Damage callsite audit scans ordinary native and this-mod callers,
verifies exact closed C# generic adapter signatures and unchanged IL boundaries,
and records exclusive existing raw-skill-wrapper ownership. It also checks the
prepared reflection bridge, central skill-wrapper routes and real no-pending
native Task identity. `damage-callsite-catalog.json` lists every actual source;
the native factory and generic entries are not detoured. Runtime caller counts
differ, so stable and beta totals differ while the same bond cases run in both.
