# 多段技能破坏率预算回归

验证 DragonArrow、BladeDance、MagicSwordLuckySeven 改为一次多段 `AttackCommand.Execute` 后，卡牌边界只分配一次完整的破坏率预算，随后按最终攻击段数分摊。

当前默认技能系数为 **3**。完整技能的破坏率预算为「敌人系数 × 3」，再按最终段数分摊。

被测 DLL：`outputs/destruction-coefficient-3/stable/bin/Release/HBRGOSakiMod.dll`

SHA-256：`1BBCF18A96F13115069207CF3BB20E810611C84BEECA7085F9BEDA96B299D088`

稳定版与 beta 原生 profile 使用同一份稳定版编译的候选 DLL，各通过 **403 项**检查；两个夹具构建均为 0 警告、0 错误。

报告：

- `outputs/destruction-coefficient-3/validation/stable/report.json`
- `outputs/destruction-coefficient-3/validation/beta/report.json`

覆盖实际 `HighHitDamageVisualHelper.BeginCardBatch`、`BeginAttack`、`ResolveHitCount`／`SetResolvedHitCount`，以及原生 `BeforeAttack`、`ModifyAttackHitCount`、`HbrDestructionGainHelper` 与 `DestructionRatePower.AfterDamageReceived`。

- 三张牌在无连击、小连击、大连击、三类同时存在时，默认技能系数均为 3，分摊段数为最终的实际段数。
- 全部段数命中 HP 时，总破坏率增长为普通怪 6、精英 15、Boss 15、FlatHand 18 个百分点，不会再被原循环次数额外除一次。
- DP 段与全额格挡段占用对应分摊预算，但不增长破坏率。四段中前两段分别为 DP 和全挡、后两段命中 HP 时，仅获得完整技能预算的一半（普通怪为 3 个百分点）。
- 单段的重复领取路径和重复接收回调不能领取第二次预算或再次乘以 3，超出最终段数的领取不能增加总预算。
- 无技能 scope 时，有 hit scope 与完全没有 scope 两条路径均使用默认系数 3；hit scope 仍只能领取一次。
- 显式 `BeginSkill(..., skillCoefficient: 0.5m)` 保留覆盖值 0.5，不再乘默认的 3；普通怪四段合计为 1 个百分点。

该夹具复用 `../hbr-fragile/Program.cs` 的无场景战斗设置。实际执行共享预算、分摊与接收回调；未运行完整伤害命令、卡牌 `OnPlay`、Godot 画面或多人会话。

历史记录：默认系数为 1 时，候选 `2D674B89E936C859F7185ED8E8B6FD5BAFB8A988B680CE702D7C6697F5F1652C` 在两个 profile 各通过 341 项。该阶段普通怪／精英／Boss／FlatHand 的完整技能增长分别为 2／5／5／6；旧报告保留在 `outputs/od-auto-cannon/destruction-validation/`，不代表当前系数。

重跑命令（从仓库根目录执行）：

```powershell
dotnet build validation/hbr-destruction-budget/Checks.csproj -p:ModAssemblyPath=<candidate.dll>
dotnet validation/hbr-destruction-budget/bin/Debug/net9.0/Checks.dll <candidate.dll> <sha256> <stable-profile> <GodotSharp.dll> <stable-report.json>

dotnet build validation/hbr-destruction-budget/Checks.csproj --no-restore -p:BetaNativeChecks=true -p:ModAssemblyPath=<candidate.dll> -p:NativeAssemblyPath=<beta-sts2.dll> -p:NativeRitsuAssemblyPath=<beta-STS2-RitsuLib.dll> -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hbr-destruction-budget/bin/Beta/Checks.dll <candidate.dll> <sha256> <beta-profile> <GodotSharp.dll> <beta-report.json>
```
