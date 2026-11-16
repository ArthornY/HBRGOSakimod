# 31E native behavior validation

Run the stage `run-suite.ps1` with `-Suite 31e-bond` for stable and then beta. Each report binds the exact immutable candidate DLL SHA and the runtime profile manifest.

The host uses real `CardModel.OnPlayWrapper`, native `CardCmd.AutoPlay`, real energy spending and pile state, native hit-count hooks, combo ordering and native SavedProperties. It covers permanent roster deduplication, first-attack preview/consumption, different-name draw-pile calls, no recursive calls, empty candidates, six arrivals, ordinary/personal/native turn identity and return to the suspended ordinary turn. The follow-up 69 cases exclude Unplayable Yotsuha from calls and count her actual hand-end effect as the sixth arrival.

Only wait/texture/pile-animation sinks are isolated. Personal-turn requests are recorded at the existing coordinator entry; its multiplayer queue/session lifecycle is verified separately. This is managed source behavior validation, not a rendered Godot or live multiplayer test.

最终重绑：stable `BA34BE76868F1480196336C0D2BB46B4B5DCEA0EAD9000BB6BECF23E7E358E03`、beta `E588AF3C80A134724CF4DDE161F8373C2C67AB5F87FF6B5F858E53A330ADE24E`；两份 `31e-bond-final-report.json` 均通过。完整索引为 `outputs/31d-and-monsters-20261115/owned-final-suite-summary.json`。
