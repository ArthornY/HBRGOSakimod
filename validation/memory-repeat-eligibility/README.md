# Repeat-memory eligibility and gold

This host links the existing independent-story fixture without editing it or
executing its old whole-story/repeat assertions. It executes actual native
`EventModel.ToMutable/BeginEvent`, received personal-choice messages,
`EventOption.Chosen` and pending task completion against production parsed GYM
JSON and the production encounter/reward/page code.

The relevant contract is knowledge at entry to this node, before its choices
unlock new characters. A new encounter receives the original sense/card-choice
reward and finishes without an extra repeat-card step. A previously encountered
character remains eligible even if its card was never purchased. The checks
cover owner isolation, entry snapshot idempotence, existing successful-claim
receipts, native `SavedProperties.From` / JSON / `Fill` and re-entering the same
node, and a distinct later node.

All six actual map memory definitions are personal. A separate controlled
definition adds one valid, untaken combat choice to exercise the real shared
predicate and each participant's independent qualification; it restores the
production definition cache afterwards. This is not a network integration test.

The gold checks execute the native gold hook with actual manuals and a real
permanent deck supplier: Talent 0 and 99 do not amplify gold, military pay adds
its own 15% once, and the supplier neither affects another owner nor survives
its own real deck removal.

Only scene/formatting/logging/flash and original reward-selection UI terminals
are isolated. Original card rewards are actual owner-bound `CardReward`s.
The repeat-card claim/upgrade selection UI and full run serialization are not
executed; only the real manual saved properties are round-tripped. No Godot,
installed-file, RNG or delivered-file changes are performed.

Build stable then beta with explicit native references; bootstrap arguments and
the active `rebind-owned.ps1` runner match `monster-preload-intents`. The two
standalone project builds have zero warnings and errors. Production build
validation and these managed checks are separate from an in-game test.
