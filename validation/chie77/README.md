# 可爱天下无敌（需求 77）

`run-suite.ps1 -Suite chie77` exercises production card bodies and real native `OnPlayWrapper`, `GeneratePlayCount`, per-play Before/After hooks, automatic play and `PowerCmd` applications. It does not replace the wrapper with a simulated replay loop.

The suite verifies permanent, owner-local named-RNG assignments; no reroll on query or load; assignment after Chie is removed; physical copies versus OD mirrors; native SavedProperties → JSON → Fill; battle-only dressing and last judgment; hand/draw selection and graduated exclusion; upgrades; exact threshold comparison; modifier-independent judgment; normal/Heart replay composition; nested automatic play of the same card; one original energy payment; actual-last-play Pride; suspended native count continuation; and cleanup when cancellation or death prevents the bonus play.

The wrapper uses a test garden card whose body records the native CardPlay supplied to it. Card selection input, texture/clock/pile presentation, waits and intent refresh are isolated leaves. This is managed source/behavior validation, without Godot rendering or a live multiplayer session. The beta bootstrap isolates the verified Sentry initialization call in its own host copy, leaving the frozen runtime profile untouched.

Current development pair: stable `43691E29F06A5968F562E70CD653F54D0666FC4C017F9AD0C4DB718A80BBCE4D`, beta `8075DE481A94EE41987175A65E3F00C2738FFA483386088DA6D0507EF5CA4CC3`, 112 checks each. Reports identify the exact tested DLL; final integration should rerun against the final frozen pair.


最终重绑：stable `BA34BE76868F1480196336C0D2BB46B4B5DCEA0EAD9000BB6BECF23E7E358E03`、beta `E588AF3C80A134724CF4DDE161F8373C2C67AB5F87FF6B5F858E53A330ADE24E`；两份 `chie77-final-report.json` 均通过。完整索引为 `outputs/31d-and-monsters-20261115/owned-final-suite-summary.json`。
