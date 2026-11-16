# Garden upgrade points

This independent managed host exercises the production methods on real native models and permanent `CardPile` objects. It compiles only its own executable; it does not build the main mod, invoke Godot, export assets, or change delivered files.

The final run contains 238 assertions for each native profile. Both use the exact same stable-built mod candidate, matching the intended cross-version delivery:

- Stable native 0.107.1.
- Original native beta 0.113.1, without telemetry rewriting or dependency modification.

Coverage includes combined allocated/unallocated six-sense totals and 60-point thresholds, one-time conversion of old 30-point receipts, preserved spent/earned balances, next new milestone grants, native `SavedProperties.From → JSON → Fill` for the base and both derived manuals, manual replacement, current-character starting deck baselines, an independent seven-card character, permanent deck high water, bulk additions, removal/re-add farming prevention, owner/pile isolation in the actual permanent-pile hook, 128-way concurrent synchronization, synchronous observer reentry, and unchanged room/spend/refund/saturating-balance behavior.

The three real characters currently start with ten cards. The seven-card test character and counted real getter verify that production reads the actual `Character.StartingDeck` once and persists it rather than hardcoding ten or enumerating it on every refresh.

Controlled leaves are Godot `StringName`, logging, relic flash, and unrelated post-pile team/garden/story refreshes. The points computation, receipt migration, claim, inheritance, pile callback, and native save serialization remain production code. This is source/managed validation, not an in-game render, complete gameplay session, or network test. Existing saves acquire deck-growth entitlement from their current permanent size against the original starting deck; previous larger deck sizes were never recorded by the old version and cannot be reconstructed.

Run from the repository root:

```powershell
powershell -NoProfile -File validation/memory-garden-points/run-suite.ps1
```

The bootstrap validates the pinned candidate and all profile-manifest file hashes before loading them. Report output records both the mod and original native SHA-256 values. Development reports are retained separately from the final report filenames.
