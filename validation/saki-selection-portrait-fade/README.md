# 选牌立绘时序与布局验证

独立的 .NET 9 控制台项目，直接链接 `Core/Nodes/Combat/SakiSelectionPortraitFade.cs` 与 `SakiSelectionPortraitLayout.cs`，无需 Godot、游戏或额外 NuGet 包。

在此目录运行（PowerShell）：

```powershell
$env:NUGET_PACKAGES = 'C:/Users/ROG/.nuget/packages'
dotnet restore Checks.csproj --source 'D:/stsmod/Godot_v4.5.1-stable_mono_win64/GodotSharp/Tools/nupkgs' -p:NuGetAudit=false
dotnet build Checks.csproj --no-restore
dotnet ./bin/Debug/net9.0/Checks.dll
```

覆盖范围：

- 透明度时序：半秒渐入／渐出、取消选牌立即渐出、出牌后一秒保留、保留期间继续未完成的渐入、重选取消待执行渐出、渐出途中按当前透明度反向、连续多次反向、重复事件、Reset 立即隐藏、长帧跨过保留结束时刻。
- 空闲判定：选中、保留与渐出期间都不跳帧，渐出结束后才跳过。
- 尺寸：身体占 UI 高度的 700/1080；带武器的宽立绘不超过 UI 宽度的 30%。分别检查 1080p、720p、4:3 和 21:9 下的普通、双剑、镰刀形态。
- 横移与交叉淡化：淡入时从左侧 40px（按 UI 高度换算）缓出到边缘；形态变化时 0.25 秒交叉淡化，并检查取值范围的钳制。

所有检查使用确定的时间和尺寸，无需真实等待。后台预载、Godot 节点层级、选牌事件接线、mipmap 和实际画面，仍由源码构建及游戏实测验证。
