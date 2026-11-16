# 连击能力托管验证

该夹具验证「连击（大／中／小）」的原生能力生命周期及共享攻击段数路线，不启动 Godot、不导出 PCK、不生成发布包。

## 当前候选与报告

当前规则的每版候选和原生程序集哈希、断言与结果，记录在 `outputs/hbr-effect-framework-20261114/{stable,beta}/combo-report.json`。Bootstrap 校验候选 DLL 和运行 profile 后加载；旧158项报告对应更早的合并规则，不代表当前覆盖规则。

2026-11-14 最终候选复验：stable 和 beta 各通过 **193 项**，对应 DLL SHA256：

| Profile | Candidate SHA256 |
| --- | --- |
| stable | `495E550A68EE2000E5D5F4C56ED8FFC1FD22FF338FB553D014349D541C53903B` |
| beta | `597318EB2C34FD2E566F3331B84EF160DF5336F059BB6811E2180A98B68CCB0D` |

## 覆盖范围

- 实际 `PowerCmd.Apply`、`ModifyAmount` 与原生 `BeforeAttack`／`ModifyAttackHitCount`／`AfterAttack` 调度。
- 大：段数翻倍，每次获得 1 次；中：段数 +3，每次获得 3 次；小：段数 +1，每次获得 5 次。
- 同时存在时按「先加段数，再翻倍」计算；与旧 Legend、旧幻影翻倍能力同时存在时保持排序。
- 无论一次攻击命中几个目标、几个段数，每类连击均只消耗 1 次。空命中、预览、纯 Unpowered 伤害不消耗。
- 每种最多两份独立实例；第三份按共同规则替换较弱的（连击强度固定，所以选择剩余次数较少者），重置为新次数而非相加；同样少时替换先获得者。普通层数修改不创建新份；攻击中获得的新份不被旧攻击消耗。
- 嵌套攻击隔离、克隆无共享预留、不同持有者隔离、重复完成幂等。
- direct scope 的取消与嵌套处理，以及原生攻击异步故障／同步 finalizer 后的预留释放。
- 实际 `AttackContext` 记录与回放；混合 powered／Unpowered 段数时只复制 powered 段数。
- 实际 `PhantomHelper.BuildAttackSegments` 与 `PerformAttackIntent` 逐段循环的顺序、预览与命中标记。
- 未来连续行动预览按 1／3／5 次逐步失效。
- 实际刚射、乱舞、龙之箭、连斩、刮奏的 `OnPlay` 与原生 `AttackCommand.Execute`，整张牌只消耗一次连击。
- 连斩每段伤害相同：六感抗性已删除（2026-11-09），旧的抗性能力在两次命中间变化也不再影响伤害；刮奏保留每个目标的独立脆弱加伤。
- 不同伤害的攻击工厂回放：小连击将 `[10,20]` 变为 `[10,20,20]`，大连击变为 `[10,20,10,20]`；资源伤害工厂每次重新进入绝对基础伤害上下文，整段序列只消耗一次连击。

本夹具复用 `../hbr-fragile/Program.cs` 的无场景世界设置；日志、`StringName`、监听器枚举采用确定性测试边界。`CreatureCmd.Damage` 只替换场景与伤害执行部分，记录真实接入路线发出的原始段数及目标；它的 prefix 排在生产记录 prefix 之后，保证生产回放机制被实际执行。未覆盖完整游戏画面、完整出牌动作调度、完整浮游炮行动、完整存档加载或多人会话。

## 重跑

在仓库根目录执行，替换候选路径和 SHA 后可复用：

```powershell
dotnet build validation/hbr-combo/Checks.csproj -p:ModAssemblyPath=<candidate.dll>
dotnet validation/hbr-combo/bin/Debug/net9.0/Checks.dll <candidate.dll> <sha256> <stable-profile> <GodotSharp.dll> <stable-report.json>

dotnet build validation/hbr-combo/Checks.csproj --no-restore -p:BetaNativeChecks=true -p:ModAssemblyPath=<candidate.dll> -p:NativeAssemblyPath=<beta-sts2.dll> -p:NativeRitsuAssemblyPath=<beta-STS2-RitsuLib.dll> -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hbr-combo/bin/Beta/Checks.dll <candidate.dll> <sha256> <beta-profile> <GodotSharp.dll> <beta-report.json>
```
