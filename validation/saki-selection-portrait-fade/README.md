# 选牌立绘透明度时序验证

独立的 .NET 9 控制台项目，直接链接 `Core/Nodes/Combat/SakiSelectionPortraitFade.cs`，无需 Godot、游戏或额外 NuGet 包。

在此目录运行（PowerShell）：

```powershell
$env:NUGET_PACKAGES = 'C:/Users/ROG/.nuget/packages'
dotnet restore Checks.csproj --source 'D:/stsmod/Godot_v4.5.1-stable_mono_win64/GodotSharp/Tools/nupkgs' -p:NuGetAudit=false
dotnet build Checks.csproj --no-restore
dotnet ./bin/Debug/net9.0/Checks.dll
```

覆盖半秒渐入／渐出、取消选牌立即渐出、出牌后一秒保留、保留期间继续未完成的渐入、重选取消待执行渐出、渐出途中按当前透明度反向、连续多次反向、重复事件、Reset 立即隐藏以及长帧跨过保留结束时刻。所有检查使用确定的单调时间，无需真实等待；Godot 节点层级、选牌事件接线与实际画面仍由源码构建及游戏实测验证。
