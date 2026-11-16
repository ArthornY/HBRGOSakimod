# 31D bond behavior host

Use `outputs/31d-and-monsters-20261115/run-suite.ps1 -Suite 31d-bond -Profile stable` and then the beta profile with `-CheckArgs @('--isolate-telemetry')`. Each run records the exact immutable candidate SHA in its JSON report. The stable archived runtime has no telemetry module initializer; only beta needs the verified initializer isolation.

The checks call the production relic's actual turn handlers and native `PowerCmd`. They cover zero/one-member suppression, distinct permanent members, each own-turn increment and cap, duplicate callbacks, personal-turn/resumed-normal-turn isolation, native extra turns, sixth-turn Morale and draw receipts, allied players and their independent phantoms, the existing level-ten Morale cap, owner-only draw, hero/pet damage through the shared pipeline, and native SavedProperties JSON reload. Mid-turn roster loss checks the effective damage, icon counter and formatted power/relic descriptions together. Map presentation checks execute the pure badge geometry and reward-recipient formatter, then verify UI wiring and icon names.

The host isolates draw scheduling, engine texture loading, HUD refresh and visual waits. It does not boot Godot, render a map or exercise live mouse/controller input. It does not validate the scouting engine itself; that is the separate `31d-scouting` host.
