# 星癌体名单原生验证（需求78）

直接调用生产 `HbrEnemyCancerHelper.IsCancer`，不用测试替代判断。独立写出的39类预期与运行时全部具体 `IHbrgoMonster` 比较，真实 `ModelDb.Inject` 注册怪物／遭遇，再运行四幕真实 `GetAll()` 与 `AllPossibleMonsters`，保证完整覆盖当前表怪及四个独立Boss。

每类检查敌方、死亡、我方、玩家本体、宠物、幻影、状态不改变种类与敌方小怪；还枚举当前原版程序集全部具体怪物并逐个确认排除。明确区分友方 `RedCrimson` 与敌方 `RedCrimsonBossMonster`。重复查询不增加Power／图标、不改资源，也不依赖正在战斗。

输出 `hbr-enemy-cancer-report.json` 绑定不可变生产DLL和原生程序集SHA，包含逐类四幕来源表。仅隔离Godot StringName构造和日志输出；不替代真实分类、工厂、遭遇、属性或伤害逻辑。不宣称进行了Godot场景或真实联机验收。

用新阶段 `run-suite.ps1 -Suite hbr-enemy-cancer -Profile stable/beta -Candidate <immutable DLL>`，beta加 `-CheckArgs @('--isolate-telemetry')`。同项目两档顺序运行。完整面向用户的名单见 `docs/hbr-enemy-cancer-roster.md`。

开发候选执行结果：stable `B92923A3B771C809E9712C8D01E5CE801A79D134F320A526FA44D5C3BC76FCD2` 694项，beta `B7EF633533947C817D9F36FB6CDF401FA0E06D1A89FAFF297C9A615BC8DF6DCB` 696项，全通过。两档原生具体怪物类数相差2，造成检查数差异；四幕39类清单一致。结果索引 `outputs/31d-and-monsters-20261115/req78-enemy-cancer-development-verified.json`；最终批次仍需重绑。

最终同候选绑定已完成：stable `BA34BE76868F1480196336C0D2BB46B4B5DCEA0EAD9000BB6BECF23E7E358E03`、beta `E588AF3C80A134724CF4DDE161F8373C2C67AB5F87FF6B5F858E53A330ADE24E`；见 `outputs/31d-and-monsters-20261115/owned-eight-final-index.json`，逐报告验证进程0／SHA／各自schema，开发候选记录保留作为历史证据。
