# Ritsu per-hit / 31B coexistence checks

This host installs the real Ritsu `AttackCommandExecuteHitHookPatch.Transpiler` under its actual `com.ritsukage.sts2-RitsuLib.framework-core` Harmony owner and the actual production 31B class processor. It exercises both installation orders and a subsequent Harmony rebuild in fresh native-profile processes.

The fix preserves Execute's Ritsu wrapper, then adapts only two exact Ritsu native-Damage leaves: the context-null fallback and the async command after `BeforeAttackHit`. A real PowerModel implements Ritsu's `IAttackHitHookListener`; its before/after callbacks run through the real dispatcher, with a damage mutation. The host checks hit metadata, pending-effect timing, typed target VFX, before-hit retargeting away from the shield holder, cancellation before damage and suspended context-null fallback. The engine damage terminal is controlled. Full native attack presentation, Godot rendering, live multiplayer and packaging are outside this suite.

The previous delivered DLL is a negative control: actual Harmony composition reproduces Ritsu's zero-match failure in either installation order. Its report has `negative=true`; passing means the expected failure was observed, not that the old DLL is compatible.

Reports and complete immutable/native hashes are indexed in `outputs/startup-1131-fix-20261115/ritsu-hit-validation-summary.json`. The current 0.113.1 managed initializer is used unchanged; the old 0.113.0-only telemetry initializer is isolated solely in an output copy for that historical profile, with the existing exact-byte proof. No installed DLL is modified.

Projects link historical fixture sources to reuse their initialized mutable Player/Combat/ModelDb setup; their existing nullable warnings are not suppressed. Production compilation is performed separately by the root agent. Initial fixture failures and the discarded before-wrapper design remain in the output evidence folders.
