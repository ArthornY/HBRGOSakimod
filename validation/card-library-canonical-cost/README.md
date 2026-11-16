# Canonical card-library cost regression

This managed host installs the four production `CardEnergyCost.GetWithModifiers` patches: Ichiko, Hotaru, Monaka and Yamawaki. It constructs real `ModelDb` canonical Cruelty, BlackHole, LetMeShowYouClass and HotaruAddLight prototypes, calls their actual costs and the actual native `NCardGrid` ascending/descending cost comparers. A direct canonical `Owner` read must still throw, proving the fix did not disable native mutability assertions.

The mutable case uses an owned hand card, a real permanent-deck Frugal supplier and native local cost modifiers. One controlled global-cost Hook leaf records its input and subtracts one: 10 → Frugal 5 → local +2 = 7 → global 6. Removing the real deck supplier must invalidate the cache and restore the local-only result to 12.

Run `outputs/card-turn-stall-1131-20261115/run-canonical-cost-checks.ps1 -Profile stable` and then `-Profile beta`. The same stable-compiled candidate is loaded against each hash-verified native profile. An explicit `-Candidate ... -ExpectedSha256 ... -NegativeControl` runs the old delivered DLL and passes only if the real patched canonical cost throws `CanonicalModelException`.

Final evidence is in `outputs/card-turn-stall-1131-20261115/canonical-cost/index.json`: 25 positive checks per profile and six old-DCEF negative-control checks. Three earlier fixture setup failures are preserved separately; they are not final results.

Only StringName construction/logging and one global cost Hook terminal return are isolated. The real costs, canonical assertions, sort comparers, local modifier iteration, permanent Frugal query and deck invalidation run normally. The existing local NuGet `System.IO.Hashing` 9.0 dependency is loaded read-only for stable Ritsu; pinned profiles are not edited. This does not execute the complete library scene, Loadout's UI filter, rendering, Godot or packaging.
