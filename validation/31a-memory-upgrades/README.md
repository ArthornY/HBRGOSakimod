# 31A garden upgrades and shared effects

Source-only managed validation for requirements 28–35. The fixture links the existing native effect host and executes the production six 31A card bodies, selected-upgrade logic, power application/removal, formulas, instance limits and actual native turn-start hooks.

Coverage includes seven-upgrade caps (eight candidates for Megumi), base and fully upgraded card text, all upgrade descriptions and energy-icon arguments, pre/post-attack timing, exact block conversion, permanent uncapped effects, stun follow-ups, critical-range team counting, DP revival, talisman growth, debuff cleansing, extra-turn isolation, new Minds Eye arithmetic/consumption, and Yuki's native and authored multi-target application boundaries.

The new Minds Eye tests use real player/extra-turn membership and real card-play scopes. Personal sessions are populated directly in the production coordinator, then evaluated by its actual membership predicates; full network admission is covered separately by `validation/personal-immediate-turns`.

Yuki tests call the production typed `ApplyEnumerable<T>` callsite adapter, which delegates to the untouched native `PowerCmd.Apply<T>(IEnumerable<Creature>)`, for native Weak and mod Fragile/AttackDown. A real native Potion of Binding body also verifies its transpiled Weak/Vulnerable calls. Unknown external mods' direct API callsites are not automatically covered. They verify exact returned model types, target ordering, one charge shared across targets, awaited continuation, caller AsyncLocal restoration and cancellation recovery. An artificial TaskCompletionSource controls only the first native animation wait.

## Execution

Build against the exact candidate DLL and matching frozen native profile. Set `BetaNativeChecks=true` for the beta profile. Use distinct intermediate/output directories per profile. The bootstrap takes five arguments:

1. Candidate mod DLL path.
2. Its SHA256 hash.
3. Frozen dependency-profile directory, containing `profile-manifest.json`.
4. GodotSharp 4.5.1 assembly path.
5. Destination report JSON path.

Set `HBR_NATIVE_RUNTIME_DIRECTORY` to the same profile directory before running the bootstrap. The full native caller-IL catalog additionally resolves System.IO.Hashing 9.0.0 from the user's NuGet cache; its loaded path/hash is recorded separately in the report. It verifies the candidate and dependency hashes before loading the host. Final evidence lives in `outputs/31a-28-37-20261114/{stable,beta}/garden-report.json`.

## Limits

Attack execution, native stun state mutation, permanent-card removal and HP writes are explicit controlled leaves. PowerCmd and the production calculation/reservation/completion helpers run normally. Revival therefore verifies the real reservation/completion contract and actual DP reset while terminal card removal/HP assignment are isolated. Raw-critical receipt tests inject the receipt; normal roll classification has separate existing coverage. UI textures, waits, audio and rendering are not exercised. The linked legacy host has existing nullable warnings; production builds are validated separately with warnings as errors. No old-save adaptation, Godot process, packaging or upload occurs.

## Upgrade IDs

Existing IDs are retained where useful for the current model, without migration code:

- Ruka: 0 AoE, 1 Vigor, 2 next-attack grant, 3 medium combo, 4 X energy, 5 Minds Eye, 6 critical buffs.
- Yuki: seven existing IDs; 4 now converts each 100 block to 1% exact amplification.
- Megumi: 0 critical-range extension, 1 stunned damage bonus, 2 discount, 3 once-per-turn AoE stun, 4 healing inversion, 5 doubled DefenseDown basis, 6 permanent uncapped debuffs, 7 stun-triggered DefenseDown. Maximum seven selected.
- Tsukasa: seven existing IDs; base is permanent uncapped AttackUp; 2 entry energy and 5 entry team Small combo are active.
- Karen: 0 discount, 1 pre-attack team critical buffs, 2 AoE, 3 destruction ×2, 4 Break damage/destruction, 5 extra hits/refund, 6 Break-dependent discount.
- Tama: 0 one-HP/full-DP revival, 1 talisman growth, 2 one visible-debuff cleanse, 3 discount, 4 initial talisman, 5 talisman damage bonus, 6 Minds Eye plus critical buffs. Multiple permanent copies do not multiply the +2 talisman passive; each revival still consumes its own actual permanent card.
