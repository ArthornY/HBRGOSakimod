# HBR 装备特效原生验证

托管夹具，写法沿用 `validation/hbr-stat-powers`：按 SHA-256 载入指定的候选 DLL，在真实游戏程序集上运行，装上生产的伤害适配补丁，经过 `HookCompatibility.ModifyDamage` 完整伤害管线核对 ★6 饰品的战斗效果。装备通过手册存档属性 `HbrEquipmentData` 写入，和游戏里的存档格式相同。

运行（PowerShell，在仓库根目录）：

```powershell
$dll = (Resolve-Path .godot/mono/temp/bin/Release/HBRGOSakiMod.dll).Path
$sha = (Get-FileHash $dll -Algorithm SHA256).Hash
dotnet build validation/hbr-equipment-native/Checks.csproj -c Release -p:SkipModOutputCopy=true "-p:ModAssemblyPath=$dll"
dotnet validation/hbr-equipment-native/bin/Release/net9.0/Checks.dll $dll $sha libs C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll report.json
```

beta 运行时：编译时加 `-p:BetaNativeChecks=true`、`-p:NativeAssemblyPath=<profile>/sts2.dll`、`-p:NativeRitsuAssemblyPath=<profile>/STS2-RitsuLib.dll`，并用单独的 `OutputPath`、`IntermediateOutputPath`；运行时把 `libs` 换成该 profile。夹具不做遥测隔离，要用已经隔离过的 profile。

覆盖范围：

- 没有装备时，进出伤害、手册的伤害系数、破坏率、OD 和出目都和原来相同。
- 攻击耳坠最高档加火耀远古项链：对 HP 的一击在管线里正好 ×1.25（同在攻击区，相加）；10 段时耳坠降到 5%，合计 ×1.15；4 段时为 15 − 10 × 3 / 9。无力伤害不受影响。
- 目标有 DP 时攻击耳坠不生效、击碎耳坠生效，管线里两者的比值为 1.25 / 1.10；目标没有 DP 时反过来。
- 浮游炮沿用持有者的远古项链，幻影不沿用；另一名玩家的装备只影响他自己的攻击。
- 火套远古项链让敌人的一击在管线里 ×0.9，不影响自己的攻击。门系列手环在非 Boss 战不生效；Boss 战里大幅档 15% 和项链 10% 相加，为 ×0.75；无力伤害不减。
- 破坏耳坠最高档加远古项链：1 段时破坏率增量 ×1.15，10 段技能的第一段 ×1.25。
- 驱动耳坠最高档：1 段 OD 计量 ×1.05，10 段及以上 ×1.15；浮游炮沿用，幻影和敌人不沿用。
- 戒指：炎之戒指 10% → 攻击牌出目 +20，冰之戒指 4% → 技能牌 +10，暗之戒指 6% → 能力牌 +13；其他类型为 0；关闭 HBR 数值时为 0。

没有覆盖：回合开始的能量、战斗开始把基础打击变为完美打击（两者都要走完整的战斗命令流程）、Godot 界面、奖励、多人同步、游戏进程。
