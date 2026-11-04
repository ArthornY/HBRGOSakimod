# Explicit effect skill damage regression

## Shared-host patch registration regression (2026-11-04)

The current fixture passes **322 checks** against the same frozen merged production
DLL `7B6226B14AE5080BCD191E46A8F4D078CF5BD67024B00F40008959F230D0569B`
in all four combinations:

| Linked stat host | stable | beta v0.111.0 |
| --- | ---: | ---: |
| overcharge-fix: absolute hook not installed by host | 322 | 322 |
| test-pack: absolute hook already installed by host | 322 | 322 |

All four fresh fixture builds have zero warnings and errors. Reports are
`outputs/effect-absolute-fix/fixed/{overcharge,testpack}-{stable,beta}.json` in
the overcharge-fix checkout. The tested `Program.cs` SHA256 is
`94BB8B97F6CAAA54AD16A28C0A8EF1C4649EC590631555BD0BE58828672BC85A`.

The previous fixture installed `HbrAbsoluteBaseDamageHookPatch` after invoking
the shared stat host's setup. The newer host already installs it for Strike
Dummy checks. Registering it a second time under another Harmony owner executes
two `BeginHook` prefixes: the second correctly disables the nested same-intent
receipt. Thus an unknown absolute preview incorrectly *in that test assembly*
entered skill conversion (3857 instead of 6). The production guard was correct.

Fresh pre-fix controls using the same historical production DLL
`D6990D1CFD5D1B904653CD3164DD1FA981B33C6BE98F9D145F42D9627B312E6E`
passed 315 checks with the old host, but failed at check103 with the newer host.
Their reports are `outputs/effect-absolute-fix/baseline/source-overcharge-stable.json`
and `source-testpack-stable.json`. With the fixed fixture, that same historical
DLL and newer host pass322 (`fixed/testpack-historical-d699-stable.json`).

Fixture setup now queries the actual runtime hook target and exact production
Prefix/Finalizer methods. It installs only when both are absent, reuses one
complete existing pair, and fails on partial or duplicate registration. A second
setup request must retain exactly one pair. Existing resolved6 versus newly
declared3857 expectations remain unchanged; additional controls require both
preview and actual damage to return to6 after the explicit declaration ends,
for both outer absolute and precalculated scopes. No production method or
damage formula is changed, and no expected result is relaxed.

These are managed command/hook tests with the terminal HP isolation described
below. No game, package or installed-file modification is performed.

## Earlier effect-skill coverage and frozen candidates

The earlier effect-skill/Vigor candidate `4C5E94B28D29CEF3831FC4D19CAC7DB986E991DB7D1A06066E0EFE1BA61E1E3A` passed315 assertions on both stable and beta v0.111.0. Its historical reports remain in `outputs/vigor-skill-coefficient/candidate-stable.json` and `candidate-beta.json`.

The preserved pre-change DLL is `outputs/effect-skill-damage/baseline/HBRGOSakiMod.dll`, SHA256 `89AE465F1CCFAB252D5C9327F58C2F953D1687BB2164417B0338F88331DF04B7`. Its real native Kusarigama third-attack handler produced literal 6 damage where the declared 100% skill must produce 3857 for actor stats 100 against enemy parameter 150. `baseline-stable.json` records this failure before any missing-new-helper check. `intermediate-resolved-stable.json` preserves a second genuine failure from candidate `21C85851BA1BE7FEF290EE86BF664571A716021C73F62538700D8061C377B8F8`: an outer matching precalculated context incorrectly left an explicit skill preview at 6 instead of 3857. Both behaviors pass in the final candidate.

Coverage includes:

- Raw skill units 6, 2 and 3 through the actual attribute calculation, native Weak/Vulnerable, persistent HBR amplification, an active Vigor counter and a ready Pen Nib. A powered Pen Nib control really doubles damage; the original Unpowered effect gains no such bonus and leaves its marker/counter unchanged. Preview never samples RNG; actual damage uses its own critical sample independently of a conflicting parent sample. Environmental damage without a dealer uses raw units times 1000, while undeclared Unpowered and legacy damage remain literal.
- Real native Kusarigama `AfterCardPlayed` three-attack counting, target RNG and original typed DamageVar adapter; real native Thorns `BeforeDamageReceived` with Thorns on both actors, original retaliation flags and no loop. Real native Burn `OnTurnEndInHand` also passes raw 2 through its typed adapter to exact skill damage 1285; FirePotion `OnUse` passes raw 20 through its direct-decimal consumer to exact skill damage 12857. Burn retains its native Unpowered|Move flags and Burn card source; FirePotion retains Unpowered, its native dealer and null card source. Raw variables/counters and command target/dealer/card/props remain unchanged. No native handler or typed adapter is replaced by a copied algorithm.
- Claimed canonical command scopes across an actual asynchronous continuation, synchronous/asynchronous faults, same-argument unknown nested commands, same-command AoE targets sharing one independent sample, non-null card attribution, and matching outer precalculated/absolute contexts. Unknown resolved damage stays resolved, while a new explicit declaration still converts in both preview and actual command paths.
- A real `HpOnlyDamageContext.Damage` invocation and its production canonical command patch distinguish already-resolved unknown HP damage from a declared raw skill: unknown amount 1 stays 1, while explicit raw 1 uses HP weighted-stat 140 (Strength 160, Dexterity 100) for damage 928 even with target DP 2000. It differs from the DP weighted-stat 120 result 785, keeps HP-only flags/card/dealer and closes pending/current contexts without spending terminal-isolated DP.
- All four event handlers' original IL calls to the patched typed adapter, followed by execution of those exact native adapters with owned native DamageVars: 3/14/11/8 become environmental 3000/14000/11000/8000. Original DamageVar flags, null dealer/card and storage remain intact. The complete event choices/UI and subsequent event rewards are not executed. These events share the typed adapter; there is no separate event damage patch class.
- Native `LocManager`/SmartFormat formatting of Kusarigama, Burn and Thorns descriptions, including nested native diff formatting, canonical owner gates and an explicit unowned merchant display scope. The displayed 33.33% is rounded text; numerical conversion retains the raw 2/6 coefficient. Status registry/storage and exact native consumer metadata are checked for five typed adapters and three direct potion `OnUse` bodies. The other potion/status handlers and the surrounding `PotionCmd.Use` inventory/consumption lifecycle are not executed here. The merchant Harmony patch installs, but no shop node is instantiated.

The fixture links the existing stat-power minimal host, overcharge bootstrap and strict telemetry isolator without editing them. Production canonical numeric Damage Prefix/Postfix/Finalizer and the real actual-damage wrapper/full native Hook execute. A lowest-priority terminal prefix replaces only the native HP command body, returning synthetic damage results after calculation; HP/DP spending, target caps in the native damage body, the complete native `AttackCommand.Execute` body and full combat are outside this fixture; the Vigor extension below does execute native BeforeAttack/AfterAttack hooks and their real consumption. VFX, logging, energy icon pool/prefix lookup and Godot string construction are isolated host leaves; native descriptions and production skill tags/formatting are retained. No Godot rendering, game session, save/clone suite, package or installed-file changes are performed.

Run from the checkout using the pinned candidate (the default mod build output can also be passed with its verified hash):

```powershell
$candidate = (Resolve-Path outputs/vigor-skill-coefficient/candidate/HBRGOSakiMod.dll).Path
$digest = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$godot = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
dotnet build validation/hbr-effect-skill-damage/Checks.csproj -c Release -p:SkipModOutputCopy=true
dotnet validation/hbr-effect-skill-damage/bin/Release/net9.0/Checks.dll $candidate $digest libs $godot outputs/vigor-skill-coefficient/candidate-stable.json
```

Beta uses the existing verified runtime profile and a separate build output:

```powershell
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet build validation/hbr-effect-skill-damage/Checks.csproj -c Release -p:SkipModOutputCopy=true -p:BetaNativeChecks=true "-p:NativeAssemblyPath=$profile/sts2.dll" "-p:NativeRitsuAssemblyPath=$profile/STS2-RitsuLib.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hbr-effect-skill-damage/bin/Beta/Checks.dll $candidate $digest $profile $godot outputs/vigor-skill-coefficient/candidate-beta.json --isolate-telemetry
```

Candidate/profile hashes are checked before loading. Stable native SHA256 is `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`. Beta source native SHA256 is `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`; its output-local isolated copy is `0C0C1CC3DD3FFF8E2D10BDE4FDA76A5E7743402047B518B4B188A7ED8B65BFAA`. The isolator verifies the module initializer consists only of the Sentry initialization call and return, zeros just that call, proves every other byte is identical and writes proof into the report. Original runtime/profile assemblies remain unchanged. Rng counter assertions read the native public property on stable or its private backing field on beta; no RNG method is replaced.
The Miniature Cannon extension adds 43 assertions; the first 197 successful assertion strings exactly match the earlier report. The pre-change DLL is frozen at `outputs/miniature-cannon-skill/baseline/HBRGOSakiMod.dll`, SHA256 `E6615360E4D8FFAA20A5ED9FD26A6498AAAC9C892BB2091EF21EA1A86F41E661`. `baseline-stable.json` reaches its real native additive hook and fails at final damage 3860 instead of 5785: raw base 6 first becomes 3857.14, then native flat 3 was appended. The candidate converts the raw 3 addend to the independent 50% skill component 1928.57, combines the two components, and floors once to 5785.

The extension uses a real upgraded native Strike and real `MiniatureCannon.ModifyDamageAdditive`, retaining its raw ordinary `ExtraDamage` variable at 3. The complete original command executes through the actual wrapper and native Hook; no extra damage command is issued. The fixture's relic-inclusive listener prefix explicitly runs at `Priority.First`, before the linked stat host's power-only terminating prefix. An independent real `RunState.IterateHookListeners` assertion requires the cannon relic exactly once. A passive native hook postfix observes, without replacing or changing its return, exactly one actual calculation call returning 3. Reports call the listener projection `nativeListeners` and the passive observations `nativeAdditiveCalls`; neither is the native outer Hook's applied-modifier output.

Checks cover current attributes, native Weak/Vulnerable and HBR amplification after the base-plus-bonus sum; one shared successful critical sample gives base 9000 plus bonus 4500, with no second RNG draw. Raw zero still gets the independent additive skill; at enemy parameter 330, base and bonus each retain their own minimum 1, totaling 2. An active Ghost focused-breakthrough bonus of 0.36 units applies to the main base only. Non-upgraded, native non-powered and foreign-owner gates remain native. The ownership guard preserves its dealer-owner OR card-owner rule, and legacy mode still yields raw 6 + flat 3 = 9. Absolute world-space base 6 remains 6 while the extra component becomes 1928.57; its existing noncritical rule consumes no RNG even with a critical seed. Precalculated and unknown HP-only damage continue to skip the complete additive phase. The original additive stage is retained; this fixture does not add a new Madness-capability host.

Owned and explicit merchant-context native descriptions render the addend as 50%, preserve raw `ExtraDamage = 3`, and restore canonical native formatting after scope disposal. These remain managed calculation/formatting checks with the synthetic terminal HP leaf and telemetry isolation described above, not game execution or shop rendering. This extension changes only its Program and README, adds no package and touches no installed files.
The Vigor extension adds75 assertions. The first240 successful assertion strings exactly match the previous Miniature Cannon report. Its frozen baseline is `outputs/vigor-skill-coefficient/baseline/HBRGOSakiMod.dll`, SHA256 `18CBFD50AA21476E1557A9D3EFD3985E3796AEF0894EF223C9C4582CDC69D836`. `baseline-stable.json` first passes the complete prior240 checks and real native Vigor reservation/consumption checks, then fails at6857 rather than5785: the old implementation adds3000 world-space damage to base3857 instead of evaluating raw6+3 as one150% coefficient.

The extension constructs a real native AttackCommand and owned Strike, executes native `Hook.BeforeAttack`, the original Vigor additive hook through the actual command/Hook calculation, attaches the captured native command results with `AddResultsInternal`, and executes native `Hook.AfterAttack`. Three Vigor remain until AfterAttack; native consumption removes them and production reservation tracking clears. A passive observer requires exactly one effective native additive evaluation returning raw3. The canonical damage command retains input6, original flags/card/dealer, and an explicit powered-skill intent still matches its original raw6 while the numerical coefficient becomes9. No native Vigor amount, reservation, consumption or damage coefficient is directly replaced.

Checks cover actor attributes with Strength20, native Weak/Vulnerable and HBR amplification25% (final9944), one shared successful critical sample (13500), legacy6+3=9, Unpowered exclusion, zero-base plus Vigor3, and a single min1 for the combined coefficient at high enemy E. Absolute world base250 remains250 plus an independent noncritical Vigor3 component, consuming no RNG. Precalculated and unknown HP-only damage skip Vigor collection. A phantom consumes only its own Vigor3, preserving its summoner's9. Real cannon complete-action batch/attack scopes apply Vigor before one Soul factor and consume it once; a native owner-attack reservation prevents the cannon from stealing it, then the owner attack still receives and consumes its own Vigor. These execute the native hooks and production reservation machinery around a captured damage body, not full AttackCommand.Execute or a complete animated cannon phase.

Owned Vigor3 SmartDescription uses native LocManager/SmartFormat to display50% without rewriting its counter. Real native Patter.HoverTips constructs the generic six-points-to100% Vigor explanation in an explicit HBR card context, switches back to native text in legacy context, restores canonical native text after disposal, and switches to HBR again. This exercises the original card getter and HoverTip construction, with only `PowerModel.Icon` resource loading isolated to a null icon to avoid Godot ResourceLoader/native bindings. Tooltip descriptions, scope binding, factories and formatting are retained. No game rendering or tooltip UI is claimed; no new clone/save suite, package or installed-file changes are added.
