# 隐藏遗物的卡牌引用提示

真实负控：BaseLib 3.4.8 的来源提示 `ModSourceTooltip.Fold` 假定列表至少有一项，Ritsu 0.6.14 的隐藏遗物前缀则把 `Overclock.HoverTips` 置为空。卡牌通过 `HoverTipFactory.FromRelic<Overclock>()` 获取库存提示时，前者读取 `list[0]` 抛出 `ArgumentOutOfRangeException(index)`，中止原生详情页打开。旧 1.4.0.4 DLL 已在同版本依赖下复现，first-chance 栈保留了真正抛错位置。

本套件使用真实原生 getter、真实 BaseLib 来源 postfix 与 Ritsu 隐藏遗物 prefix，断言十张卡的引用使用单个原生概念提示，保留文本和 CanonicalModel，并且不枚举隐藏遗物的库存列表。覆盖 canonical、无主 mutable、owned，基础与首级升级预览，来源提示开/关，紧凑及 Shift 全文，共 809 项。普通可见遗物仍有提示，Ritsu 的库存隐藏判定仍返回空；来源提示关闭时实际隐藏遗物复数 getter 也保持为空。

纹理、日志、需要平台的可选来源设置及键位状态是受控叶，不替换卡牌或遗物的 tooltip 主体。没有启动 Godot，不读写真实配置、存档或安装文件，不构成详情页渲染、输入或联机实测。

```powershell
$candidate = 'D:/absolute/final/HBRGOSakiMod.dll'
$deps = 'D:/absolute/current-profile'
$report = 'D:/absolute/report.json'
$sha = (Get-FileHash -LiteralPath $candidate).Hash
$godot = "$deps/GodotSharp.dll"
dotnet build validation/card-relic-reference-hover/Checks.csproj --nologo -p:ModAssemblyPath=$candidate -p:NativeAssemblyPath="$deps/sts2.dll" -p:NativeRitsuAssemblyPath="$deps/STS2-RitsuLib.dll" -p:BaseLibAssemblyPath="$deps/BaseLib.dll" -p:GodotSharpAssemblyPath=$godot -p:NuGetAudit=false
dotnet validation/card-relic-reference-hover/bin/Debug/net9.0/Checks.dll $candidate $sha $deps $godot $report
```

Beta 构建加 `-p:BetaNativeChecks=true`，使用不同输出目录；包含旧遥测模块初始化的 beta 原生 DLL 运行时再加 `--isolate-telemetry`，只修改测试宿主的副本并保留证明。所有检查通过才写 `ok=true`；断言失败写 `ok=false` 并返回非零。
