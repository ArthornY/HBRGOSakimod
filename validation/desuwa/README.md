# Desuwa validation

This focused runner reuses the existing managed effect/stat fixture and executes the real card body, roll pipeline and native block commands. The bootstrap verifies candidate and native-profile hashes before loading them.

It checks exact 133%/177% coefficients (665/885 block at INT 100 before dice), final roll 75 versus 76, percentage bonuses and teammate Magic Sword sharing +77 crossing the threshold from a low raw die, reuse of one roll, per-gain preview agreement, and two distinct native block events. The actual Focused Shot interaction verifies per-gain modifiers, floor and callbacks. Retirement checks retain Quick Thinking's model ID and old upgrade while excluding it from 31O offers; the actual source/epoch filters retain 20 common, 30 uncommon, 25 rare and 2 ancient cards.

The existing self Magic Sword +77 applies to attack cards only. This skill reads the same final result as other cards; it does not add 77 on its own. The +77 regression uses the existing teammate sharing effect, which legitimately affects skills. This change does not widen Magic Sword's global eligibility.

Build `Checks.csproj` with the selected `ModAssemblyPath`, `NativeAssemblyPath`, `NativeRitsuAssemblyPath` and `BetaNativeChecks`. Run with candidate DLL, expected SHA256, dependency-profile directory, GodotSharp DLL and report path. The JSON records hashes and all assertions.

Raw-die and absent mod-initialization registration leaves are explicit fixture boundaries. Capability lookup, final-roll arithmetic and block calculation are production code. History/rendering/waits are isolated; this is not a full save-file, game UI or multiplayer test. No Godot, packaging, commit or upload occurs.

Verified on 2026-11-14: **166 checks passed on each profile**. Evidence is in `outputs/desuwa-renames-20261114/{stable,beta}/desuwa-report.json`.

| Profile | Candidate SHA256 |
| --- | --- |
| stable | `EC9DAB2EB782E3001D3837303C570FA841F863D1CE46180583F1067E49C7A039` |
| beta | `ED80FFDE48A72C64D685A0A3D1D8F4BCB85A0D170EEDAB0F68384D2EC076EE55` |
