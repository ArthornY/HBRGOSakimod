# Nullable power removal regression

The native stable and beta APIs intentionally accept `PowerCmd.Remove(null)` as a no-op. `Remove<T>` forwards a missing power as null. The released Aoi prefix dereferenced that input before the native guard.

This narrow host installs the actual Aoi, Muua and Narara removal prefixes together. It executes native direct null removal, absent generic Hotaru removal, and the real `Millsage.BeforeSideTurnStart -> ClearTurnDamage` call. A separate previous-release negative control must reproduce `NullReferenceException` on these three paths. Native removal, application and canonical assertions remain enabled.

Non-null checks execute actual removal callbacks, expiring Hotaru buff cleanup, Aoi command staging and real deferred replay. The established Aoi nullification receipt is controlled fixture state; it is not a fresh defensive-dice test.

Chihaya coverage includes six real mode `AfterApplied` cases and thirty real `EnterSelectedRepture` remove/apply cases (five old states, three selected modes, HBR/legacy). It verifies the one selected mode, amount, owner, removed prior instance and restored suppression depth. Scythe uses a30A owner; other modes use31O.

The corrected screenshot adds one actual upgraded `MimicWillActivation+` case: the real restricted-card lookup returns Dual Swords; `EnterRequiredModeBeforePlay` completes, returns true and activates the native one-layer owned mode with other modes absent. It does not execute Combo/Draw, a full choice UI or the complete card wrapper.

Final positive count:239 per runtime. Earlier233-positive reports and12-check previous-release negative controls are retained separately. Only command waits, string/log startup and preview notifications are isolated; no `Remove`, `Apply`, `AfterApplied` or `AfterRemoved` replacement is used. No full turn loop, Godot rendering, live input or in-game test is claimed. The linked older fixture sources produce23 existing nullable warnings; main strict SDK builds are separate.

An additional released-DCEF negative control now has22 checks per runtime. Besides the original absent-cleanup failure, it executes actual native Bow→Dual and Dual→Bow `PowerCmd.Apply`: the new power is inserted before `AfterApplied`, whose missing-Normal cleanup throws. It confirms both real old/new modes remain after the exception, with the new power's correct owner/amount and suppression depth unwound. This explains the reported simultaneous modes without changing any option semantics. Reports and process exits are in `null-partial-apply-old-negative-index.json`; the original12-check evidence is retained. This extra entry is negative-only and does not alter the239-check positive suite.

Final dynamic rebind command: `outputs/card-turn-stall-1131-20261115/rebind-null-positive.ps1 -Candidate <DLL> -ExpectedSha256 <SHA>`. It uses freshly rebuilt `null-checks-mimic` hosts, original runtime profiles, explicit candidate/profile hashes, and no telemetry mutation. Final evidence: `null-removal-final-index.json` in that stage.
