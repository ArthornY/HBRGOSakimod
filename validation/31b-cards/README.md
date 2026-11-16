# 31B and Yamawaki card behavior checks

This isolated managed host exercises the five new 31B garden cards against the real stable or beta game and mod assemblies. It reuses the existing native fixture, but does not alter the earlier 31A suite or run it implicitly.

The checks cover all seven upgrade choices and native costs, real PowerCmd/HBR effect applications, independent raw shield rolls and whole enemy actions, attached effects before/after damage, owner isolation, card repeats/counters, per-physical-card OD growth, HP thresholds, exact DP recovery, inherited native SavedProperties roundtrips, and the independent spirit's HP/DP/lifecycle and coexistence with a 30A main phantom. The beta case also passes real typed DamageTargetData records through the pending-effect adapter and checks its VFX fields.

Texture/UI/wait leaves, attack presentation and automatic-card scheduling are isolated. The spirit summon runs its real production helper with an explicit SmallHopper candidate and native HP calculation; scene creation and terminal replacement death are isolated. The real Phantom late-turn hook and native Adaptable death hook are invoked. This establishes managed behavior; it is not a running-game or rendering test.

Build this host with ModAssemblyPath, NativeAssemblyPath and NativeRitsuAssemblyPath pointing at a single runtime profile; set BetaNativeChecks=true for beta. Resolve the immutable assembly path and SHA from outputs/31b-monsters-20261114/<profile>/latest-candidate.json. Run Checks.dll with assembly path, SHA256, native profile directory, GodotSharp path and report path. Only beta needs the existing --isolate-telemetry test switch.

Production source builds remain owned by the root integration task. Do not launch Godot or package from this validation host.

The req48 additions exercise the real selected-target CanPlayTargeting and SpendResources paths, including a suspended PlayCardAction payment boundary, physical-card shuffle receipts and OD mirrors, 31C thresholds, and shared Fear applications. A real HotaruCannonStatsHelper reaction command with retained card source proves that only the card attack receives its dedicated HP/DP/normal-amplification/destruction bonuses. The spirit case uses the real active RollGlossary multiplier at the actual native grant hook to show that structural pet markers and the 20% entry shell never scale with dice.

Req51 adds native three/six-hit card bodies, original destruction4.8/16.8 normalization, one enemy-turn AttackDown, native Retain/save replay, real per-owner native TurnNumber plus personal/OD token retention, once-per-physical-card PersonalImmediateTurnCoordinator entry dispatch, and actual shared critical damage with20 OD-only points. The optional HBR_31B_CASE_FILTER=ninja or ninja-od development filters are explicitly recorded in reports; the final full report uses caseSelection=all.
