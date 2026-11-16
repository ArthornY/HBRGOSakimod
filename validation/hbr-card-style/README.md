# HBR 卡牌样式验证

独立的 .NET 9 控制台项目，直接链接 `Core/Nodes/Cards/HbrCardStyleLayout.cs` 与 `Core/Models/Relics/SixSenseAttribute.cs`，无需 Godot、游戏或额外 NuGet 包。贴图由 `ui-concepts\card-style-20261104\export_textures.py` 从预览导出。

在此目录运行（PowerShell）：

```powershell
$env:NUGET_PACKAGES = 'C:/Users/ROG/.nuget/packages'
dotnet restore Checks.csproj --source 'D:/stsmod/Godot_v4.5.1-stable_mono_win64/GodotSharp/Tools/nupkgs' -p:NuGetAudit=false
dotnet build Checks.csproj --no-restore
dotnet ./bin/Debug/net9.0/Checks.dll
```

覆盖范围：

- 贴图：四套卡框 × 三档稀有度、幻影描边，都存在，并且尺寸等于对应节点区域的 2 倍或 4 倍（比例不变形）。
- 卡框：卡图窗口的上半部是透明的，四周和下方的卡身是实心的；窗口右下的斜切、卡牌右上和左下的切角位置正确。幻影描边中间是空的，只在卡外发光。
- 版面：卡图比窗口每边多 2px，形状一致；标题在缎带内居中；类型牌压在卡图左下，描述从类型牌下面开始，并避开底部的斜线和菱形；较长的类型名也不会超出卡图宽度。
- 六感徽记和敌人抗性色点已删除（2026-11-09，用户「就这么删」）：牌面徽记、强化光晕、手牌聚焦、抗性色点、数值框和悬浮补丁的源码文件都不存在；战斗界面不再给卡牌和血条挂六感节点；`HbrCardStyleLayout` 里不再有徽记和色点的尺寸；`StrikePortraitPatch`、血条补丁不再刷新六感节点。原来的圆徽贴图 `images/ui/hbr_card_style/sense_*.png`、`sense_glow.png` 留在原处，没有代码再用。

- 贴图尺寸（2026-11-07，幻影图鉴徽章「好大...」之后）：
  - Godot 设 `Size` 时会把尺寸限制在最小尺寸以上，KeepSize 下最小尺寸就是贴图原尺寸。
  - 扫描 `Core` 下全部 `TextureRect` 初始化：设了贴图之后，`ExpandMode` 必须在 `Position`、`Size` 之前。
  - `PhantomRosterArt.Image` 的顺序。
  - 幻影徽章各层按比例算，和 v2 预览一致：190 时外框 212、高亮外框 240、选中光圈 234、Lv 框 70；立绘遮罩等于直径。

- 幻影图鉴圆框的深色底（2026-11-07，用户「改深色底」）：
  - 输入 48 份：合成的 7 份（白色块、半透明边、透明处的乱码颜色、贴边、极宽、极高、空图），加模组里全部可读的怪物抠图和渲染器用的静态立绘。
  - 卡图不变：`PhantomPortraitCompositor.Compose` 的输出和改动前（套件里留了一份原样的旧代码）逐字节相同。
  - 图鉴用的透明版 `ComposeCutout`：
    - 透明度就是捕获图在卡图同一取景下的双线性透明度；
    - 叠到白底上就是卡图，差不超过 1.5 级（取整造成）；
    - 源图不透明的地方保持不透明、颜色和卡图一样，白色也一样，白毛、高光不会被抠掉；
    - 透明像素底下的颜色怎么变，两种输出都不变；没有可见像素时卡图全白、透明版全透明；
    - 按 Godot 的画法模拟（直通 alpha、双线性、无 mipmap、KeepAspectCovered 进 190px 圆框），和理想的预乘结果比，平均误差不超过 0.5 级，并且比把透明像素留成黑色更接近。
  - 源码：卡牌（`PhantomPortraitPatch`）仍取白底版；图鉴取透明版，RedCrimson（两个）和 FlatHand 仍用场景裁切；缓存和渲染按样式分开，透明版的占位全透明；圆框里依次是渐变底、立绘、苍蓝火光；详情页和右侧预览用亮一档的中心色。
  - 渐变贴图按 `GradientTexture2D` 的算法逐像素算，和 v2 预览的 CSS `radial-gradient(circle at 50% 40%, #3b2a62 / #46306f, #160e2c 70%)` 相差不超过 1.5 级。

主题选择（卡池、持有者形态、幻影选择卡）、`NCard` 节点的替换与复原、类型牌的延迟排版，仍由源码构建和游戏实测验证。
