# 随机角色解锁回归

独立 net9 宿主执行真实原生 `ModelDb.AllCharacters`、`UnlockState.Characters`、`Init/InitAsRandom`、`UnlockIfPossible`。它安装实际 BaseLib 3.4.8 与 Ritsu 0.6.14 的角色发现、选择政策及解锁补丁，再安装待测模组兼容补丁，不启动 Godot。

同一 stable 候选 `AAB3B36099C9843F569FDF2258B4B6C72C509936C8D8E879B289325D92BA8ED8` 在 stable 0.107.1 下通过 54 项，beta 0.113.1 下通过 64 项。隔离随机修复前的旧候选 `0C2D1C988CBC42F59E7ED043973A3466F72AE35E53D63875F702973F354ABF04`，stable 兼容对照通过 54 项，beta 精确旧缺陷负控通过 56 项。双档宿主编译均 0 警告、0 错误。

报告位于 `D:/stsmod/outputs/random-character-unlock-20261117/reports/`：`new-stable.json`、`new-beta.json`、`old-stable.json`、`old-beta.json`。旧模式的 `ok: true` 表示准确复现预期旧缺陷，须同时检查 `expectOldDefect`；不能作为修复验收。

## 覆盖

- 全局发现目录过滤精确 `HbrgoSaki` 类型，保留派生 31O、30A、唯一 canonical Hotaru；真实 `HBRGO_SAKI` 旧 ID 仍返回原 canonical 对象，原 hidden-unlock 补丁继续不将退役角色当作已解锁角色。
- beta 独立 `InitAsRandom` 不再因退役角色误锁。外层 Visible scope 下，BaseLib 自定义角色与仅实现 Ritsu 政策的可见但禁止随机角色均不参与随机解锁门槛。
- 外层 Visible → 内层 RandomEligible → 恢复 Visible 的完整同步范围，包括原生 `UnlockState.Characters` 内部再次读取目录。隐藏但明确允许随机的角色通过 Ritsu ref-postfix 目录来源注册，用其真实 epoch 条件验证锁定与解锁。
- 原生 Silent、Regent、Necrobinder、Defect，以及 beta Fool 的真实解锁 epoch 未揭示时，随机仍锁定；真正允许随机但仍锁定的模组角色也继续阻止随机。测试不强开原生角色。
- 已缓存的 beta 随机按钮在进度改变后复用原 delegate 重新计算，并在成功时启用。普通角色保留原生解锁路径；`IsRandom = true` 的 beta RotatingCharacter 不被误处理。stable 没有 `InitAsRandom`，新可选补丁安全跳过，保留原生旧入口行为。
- 原生解锁来源异常原样传播；初始化 finalizer 恢复外层政策范围。缓存刷新首次异常保持锁定、不启用并走原生 fallback，下一次正常刷新仍可成功。
- 真实 `AllCharacterCardPools`、`AllCharacterPotionPools`、`AllCharacterRelicPools`、`AllCardPools` 与 `AllCards` 链保留 31O／30A／Hotaru 的池和共用／现役起手卡；旧 base 的卡池、药水池、遗物池仍可直接按 ID 读取，没有删除 model／pool 注册。

## 验证边界

使用真实 BaseLib `PrefixIdPatch`，在任何 ModelDb 查找前安装；通过原生构造器取得实际 ID，不手工重写 ID。BaseLib `CustomCharacters` 是受控的内存发现列表，额外 Ritsu 角色通过实际 postfix 阶段加入；Ritsu 真实 `RequireEpoch` 绑定到现有原生非角色 epoch。`UnlockState` 是按实际 epoch ID 构造的真实对象，不替换其 `Characters` getter，不手工把退役角色加入已解锁集合。

BaseLib 的 `AddCustomCharacters` 为 pass-through postfix，而 Ritsu 目录政策为 ref postfix；Harmony 的两个阶段意味着直接 `ModelDb.AllCharacters` 在 Visible scope 下仍可能被 BaseLib 后追加角色。菜单可见目录用 BaseLib 的真实 `GetVisibleCharacters` 验证，不能把 ref 阶段观察误当最终菜单目录。新随机入口使用明确的资格目录，因此仍能处理这种追加。

全局池检查构造实际 native／mod 卡牌及池类型，再通过原生 `ModHelper.AddModelToPool` 注册有限的真实 shared／starter 映射；执行原生所有目录 getter，没有伪造 `AllCards` 或池返回列表。此检查不声称初始化了整个生产 content pack。

仅隔离 SaveManager 生成解锁状态的来源、日志、图标和需要 Godot 的 property setter／Enable／Disable 叶。不读取或写入真实存档、配置；不执行渲染、真实网络或游戏菜单操作。任何非预期异常均失败，全部断言通过才写 `ok: true`。

## 运行

永久集成后使用仓库内项目；隔离复现时将 `$fixture` 换为 stage 路径。只构建测试宿主，不构建或复制主 DLL。首次构建不要传 `--no-restore`；已有还原文件时可加该选项。`--disable-build-servers` 与 `UseSharedCompilation=false` 避免本机编译服务等待。

```powershell
$repo = 'D:/stsmod/HBRGOSakiMod'
$stage = 'D:/stsmod/outputs/random-character-unlock-20261117'
$fixture = "$repo/validation/random-character-unlock/Checks.csproj"
# 隔离源：$fixture = "$stage/staged-files/validation/random-character-unlock/Checks.csproj"
$candidate = "$stage/candidates/stable/HBRGOSakiMod.dll"
$sha = 'AAB3B36099C9843F569FDF2258B4B6C72C509936C8D8E879B289325D92BA8ED8'
$godotSharp = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
$profiles = 'D:/stsmod/outputs/card-inspect-aoi-20261116/profiles'
foreach ($native in @('stable', 'beta')) {
    $deps = "$profiles/$native-current"
    $hostOutput = "$stage/fixture-host/$native/"
    dotnet build $fixture --disable-build-servers --nologo -p:UseSharedCompilation=false -p:RepositoryRoot=$repo -p:ModAssemblyPath=$candidate -p:NativeAssemblyPath="$deps/sts2.dll" -p:NativeRitsuAssemblyPath="$deps/STS2-RitsuLib.dll" -p:BaseLibAssemblyPath="$deps/BaseLib.dll" -p:GodotSharpAssemblyPath=$godotSharp -p:SkipModOutputCopy=true -p:BaseIntermediateOutputPath="obj-$native/" -p:OutputPath=$hostOutput
    if ($LASTEXITCODE) { throw 'Host build failed' }
    $flags = @()
    if ($native -eq 'beta') { $flags += '--isolate-telemetry' }
    dotnet "$hostOutput/Checks.dll" $candidate $sha $deps $godotSharp "$stage/reports/new-$native.json" @flags
    if ($LASTEXITCODE) { throw 'Random-character unlock checks failed' }
}
```

beta 遥测隔离仅修改每份报告独立目录下的宿主 native 副本；Bootstrap 先核验原 profile 清单，再记录原始 native SHA，不改游戏依赖。stable 的 Ritsu 自定义 TargetType 需要 `System.IO.Hashing` 9.0，Bootstrap 优先读 profile，并保留本机只读 NuGet 缓存 fallback 路径。

旧缺陷负控将 candidate 改为 `D:/stsmod/outputs/tomori-phantom-grounding-20261117/candidates/stable/HBRGOSakiMod.dll`、sha 改为上文 `0C2D...` 完整值，再追加 `--expect-old-random-lock`。该模式要求新随机兼容补丁不存在，并核对旧全局目录污染、beta 独立／嵌套入口误锁和缓存未恢复的具体行为；其他异常不算预期。旧 stable 是安全旧入口兼容对照，不声称复现 beta 新入口缺陷。
