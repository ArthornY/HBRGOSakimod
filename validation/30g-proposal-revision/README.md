# 30G 下一次打出与能力牌退场回归

`Checks.csproj` 使用独立宿主，复用 `30g-marriage` 的搭建代码，不执行其旧规则断言。保留原有 113 项下一次打出、费用、重放、跨战斗状态、身份及保存检查；新增 `PowerExitChecks.cs` 检查零次能力牌退场。本次同一 stable 候选 `0C2D1C988CBC42F59E7ED043973A3466F72AE35E53D63875F702973F354ABF04` 在 stable 0.107.1 与 beta 0.113.1 各通过 173 项，且每档分别在 helper 静态初始化早于、晚于 native flight Harmony prefix 安装的独立进程运行。旧发布候选 `681B8040532BDF3FFB3D47B81FEF4656F50219865CC08CCA7D70D81CB431E36F` 在两档均通过 151 项负控检查，明确复现旧缺陷。

冻结报告位于 `D:/stsmod/outputs/tomori-phantom-grounding-20261117/reports/`：`old-stable.json`、`old-beta.json`、`new-stable-before.json`、`new-stable-after.json`、`new-beta-before.json`、`new-beta-after.json`。负控的 `ok: true` 仅表示预期旧缺陷被精确复现，须同时查看 `powerExit.expectOldDefect`，不能作为修复验收。

## 行为与覆盖

- 每个普通回合、OD 回合或个人回合开始求婚；同一 token 重复通知只结算一次。
- 普通牌下一次费用减 1，整次打出的所有效果无效；仍执行原生移动、弃置／消耗和费用清理。该次完成后，下次额外打出一次，再恢复普通状态。X 费保持原生规则。
- 原生 wrapper 内的额外打出只消耗一次状态；嵌套自动打出不借用外层的一次性额外打出。
- 美也本场持续无效果，战后永久使每次打出额外打出一次。普通牌未消耗的两个阶段通过真实物理牌身份、遗物台账及原生保存属性跨战斗保留。
- 卒灯卡的手动打出与外部自动打出均保持零次真实 OnPlay、零歌唱能力增益，经过真实 Power `RemoveFromCombat` 完成模型移除并获得恰好 1 次心动。已存在的卡面只开始一次退场。
- 普通灯卡真实首回合自动打出保留 `SingTogetherPower = 1` 与 `PreparationPower`，不处于高速攻击批次，也不重复播放。卒牌的被动监听器被原规则过滤，不将“已卒灯卡自行首回合自动触发”当作预期行为。
- 相同模型 ID 的另一张牌、同一持有者的另一物理副本及另一持有者的副本不被目标退场修改。
- 真实高速批次中连续无节点调用不制造飞行；已有未被占有节点保留原生飞行；已有 flight 占有的精确节点不会再创建飞行。非 Power 仍按原生弃牌流程处理。
- 原 `GeneratePlayCount` 任务故障原样抛出，不开始飞行、不消费状态；视觉飞行任务故障不阻断同步的零效果结算与心动状态转换。

## 验证边界

宿主执行真实 `SpendResources`、`CanPlay`、`AutoPlay`、`OnPlayWrapper`、`GeneratePlayCount`、Power `RemoveFromCombat`、灯卡 `OnPlay`、歌唱能力应用、30G helper/patch 和保存属性路径。共享宿主不执行 content-pack 注册，因此为灯卡物理牌及战斗镜像附上生产同款 `ThirtyAKeywordCapability`，供真实原生移动 hook 同步收藏升级记录。

`GetUnclaimedTableCard` 是明确隔离的整个 UI 叶：按实际 `CardModel` 对象引用返回不经过构造器的 `NCard` token，并模拟已有 flight 对精确节点的占有。private native `PlayPowerCardFlyVfx` 是计数和完成叶。两种静态初始化时序验证生产 `PrivateAccess.MethodDelegate` 确实路由到后来安装或已安装的 native Harmony prefix；没有替换生产 helper 的 delegate 字段。高速分支直接执行生产 prefix，使用真实 enabled live batch。

实际候选 IL 另检查 Power／owner alive／combat ending、`FindOnTable`、节点有效与树内／删除状态、native flight 类型和精确 `CardNode` 引用比较。此静态结构检查不等于真实 Godot 节点的动态 guard 验收。实际 tween、渲染、卡面对象释放、双端网络和游戏输入仍需游戏内复测。时钟、纹理、场景 pile 移动、等待与插入回合 token 使用原共享宿主的受控叶。

所有断言通过后才写 `ok: true`；任意非预期 fixture 或 getter 异常均失败，不把异常当作旧缺陷。

## 运行

永久集成后使用仓库内项目；隔离重现时仅把 `$fixture` 换成下方 stage 路径。`RepositoryRoot` 同时提供共享源码及依赖路径；宿主输出位于仓库的 `outputs` 下，满足共享运行时向父目录寻找 `HBRGOSakiMod.csproj` 的约束。只构建宿主，不构建或复制主 DLL。

```powershell
$repo = 'D:/stsmod/HBRGOSakiMod'
$stage = 'D:/stsmod/outputs/tomori-phantom-grounding-20261117'
$fixture = "$repo/validation/30g-proposal-revision/Checks.csproj"
# 隔离源：$fixture = "$stage/staged-files/validation/30g-proposal-revision/Checks.csproj"
$candidate = "$stage/candidates/stable/HBRGOSakiMod.dll"
$sha = '0C2D1C988CBC42F59E7ED043973A3466F72AE35E53D63875F702973F354ABF04'
$godotSharp = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
$profiles = 'D:/stsmod/outputs/card-inspect-aoi-20261116/profiles'

foreach ($native in @('stable', 'beta')) {
    $deps = "$profiles/$native-current"
    $beta = if ($native -eq 'beta') { 'true' } else { 'false' }
    $hostOutput = "$repo/outputs/tomori-phantom-grounding-20261117/fixture-host/$native/"
    dotnet build $fixture --nologo -p:RepositoryRoot=$repo -p:ModAssemblyPath=$candidate -p:NativeAssemblyPath="$deps/sts2.dll" -p:NativeRitsuAssemblyPath="$deps/STS2-RitsuLib.dll" -p:BaseLibAssemblyPath="$deps/BaseLib.dll" -p:BetaNativeChecks=$beta -p:SkipModOutputCopy=true -p:BaseIntermediateOutputPath="obj-$native/" -p:OutputPath=$hostOutput
    if ($LASTEXITCODE) { throw 'Host build failed' }
    foreach ($order in @('after', 'before')) {
        $flags = @()
        if ($native -eq 'beta') { $flags += '--isolate-telemetry' }
        if ($order -eq 'before') { $flags += '--initialize-helper-before-flight-patch' }
        dotnet "$hostOutput/Checks.dll" $candidate $sha $deps $godotSharp "$stage/reports/new-$native-$order.json" @flags
        if ($LASTEXITCODE) { throw 'Power exit checks failed' }
    }
}
```

默认先安装 private flight prefix，再显式初始化 helper；`--initialize-helper-before-flight-patch` 对调这两步，必须使用独立进程。beta 的遥测隔离只修改每份报告独立目录中的宿主原生副本，Bootstrap 校验并保留原始 native SHA 和 profile 清单；不触真实配置或保存。此共享宿主编译存在 23 条原有 nullable 警告，新增 PowerExitChecks 无编译警告；主候选的严格构建由主任务独立验证。

旧缺陷负控仅供同一受控宿主对照：将 candidate 与 sha 换为上文冻结的 681B 旧包（`D:/stsmod/outputs/package-1405-narara-20261116/candidates/main/HBRGOSakiMod.dll`），运行时追加 `--expect-power-exit-missing`。该模式要求新 helper 确实不存在、卒灯卡零效果／正常移除／heart1、零飞行且目标视觉 token 留存，并确认旧高速分支仍错误抑制现有节点；任何其他异常均失败。更新后的生产候选不能使用此开关。
