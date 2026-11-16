# Natsume / Condemnation validation (requirement 84)

Run through `outputs/31f-and-31x-83-88-20261115/run-suite.ps1 -Suite 31f-natsume -Profile stable` (or `beta`). Candidate and native-profile hashes are verified before execution; profiles share project intermediates and must run sequentially.

The host runs real native `AttackCommand.Execute`, its pre-`AfterAttack` callsite, normal native kill/death hooks, and native revival behavior. It controls terminal damage amounts and visual leaves only. It checks four physical hits at 47%, seven upgrades, source-buff consumption, first-card receipts across ordinary/OD/personal turns, family hand counting, exact-command/direct-damage boundaries, and authoritative large HP.

Condemnation is tested at both 10% and 20% thresholds with DP ignored, native multi-target order, boss/minion behavior, actual fatal credit before normal attack listeners, and permanent kill-growth integration. Reviving native targets do not receive final-kill credit until a true final death. Nested unrelated kills cannot borrow the parent card's credit; canceled native death flows must restore all scopes.

No Godot process, visual rendering, network, packaging, save migration, or release files are involved. Final reports are stored in the current 83–88 phase and explicitly bind both candidate hashes.
