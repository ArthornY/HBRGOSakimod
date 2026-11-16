# Power 本地化命名空间覆盖

宿主在任何 `ModelDb.GetId` / `Inject` 前安装真实 BaseLib `PrefixIdPatch`。246 个具体 Power 的 ID 均来自真实原生 `ModelDb.GetId`（包含 6 个原生 Strength/Dexterity 的子类）；52 个带命名空间的 Power 逐项与元数据审计计划比对。Canonical 实例由原生构造器创建，Owned 由原生 `ToMutable` 克隆，不反射改写 ID。

逐项调用实际 `Title`、canonical/owned `Description`、`SmartDescription`、`RemoteDescription`，再用真实 LocString 和 SmartFormat 格式化；只用原生 AddDumbVariablesToDescription 和模型自己的参数，不补造通用占位值。英语缺少旧裸文本的项目保留为明确跳过，不为测试编造翻译。

`ChargeUpSlashTemporaryStrengthPower` 的裸 title/description 缺键不是缺陷：其原生 TemporaryStrengthPower getter 使用 OriginModel 卡名和原生 TEMPORARY_STRENGTH_POWER 文案。实际 ID 覆盖也包含这类原生父类的派生模型，避免仅遍历内部继承链漏计。

`--overlay` 只在测试 LocTable 中叠加 alias-suggestions.json 中的原文别名，且验证原裸键内容与计划逐字相同；生产 JSON 不写入。无 overlay 可做旧数据负控；`--expect-missing` 要求读取的 powers.json 原始字节 SHA 与审计计划一致，且只出现 Diamond/Faintness 的 14 个精确预期缺键组合才返回 0。任何构造、getter 或格式化错误都使负控失败。`--minimal` 先运行 Diamond/Faintness 的完整 getter，再扩展全部 namespaced Power。

正式回归应读取已合并的生产本地化，使用本目录 `model-ids.json`，不带 `--overlay` 或 `--expect-missing`。此时计划文件只校验实际 246 个具体模型与 52 个命名空间 ID，不要求修复后的文案仍与旧审计计划一致。`--overlay` 专用于保留旧数据的受控因果演示，仍使用完整旧审计提案 alias-suggestions.json。

```powershell
$repo = 'D:/stsmod/HBRGOSakiMod'
$stage = 'D:/stsmod/outputs/power-localization-tainted-20261116'
$deps = 'D:/stsmod/outputs/card-inspect-aoi-20261116/profiles/beta-current'
$candidate = "$repo/build/HBRGOSakiMod.dll"
$sha = (Get-FileHash -LiteralPath $candidate).Hash
$project = "$stage/staged-files/validation/power-localization-coverage/Checks.csproj"
$modelPlan = Join-Path (Split-Path $project) 'model-ids.json'
dotnet build $project --nologo -p:RepositoryRoot=$repo -p:ModAssemblyPath=$candidate -p:NativeAssemblyPath="$deps/sts2.dll" -p:BaseLibAssemblyPath="$deps/BaseLib.dll" -p:NativeRitsuAssemblyPath="$deps/STS2-RitsuLib.dll" -p:GodotSharpAssemblyPath="$deps/GodotSharp.dll" -p:NuGetAudit=false -p:OutputPath="$stage/test-bin/beta/"
dotnet "$stage/test-bin/beta/Checks.dll" $candidate $sha $deps $repo "$stage/alias-suggestions.json" "$stage/negative-zhs.json" zhs --expect-missing --minimal --isolate-telemetry
dotnet "$stage/test-bin/beta/Checks.dll" $candidate $sha $deps $repo "$stage/alias-suggestions.json" "$stage/positive-zhs.json" zhs --overlay --isolate-telemetry
# 已合并本地化后的正式正测：
dotnet "$stage/test-bin/beta/Checks.dll" $candidate $sha $deps $repo $modelPlan "$stage/current-zhs.json" zhs --isolate-telemetry
dotnet "$stage/test-bin/beta/Checks.dll" $candidate $sha $deps $repo $modelPlan "$stage/current-eng.json" eng --isolate-telemetry
```

Stable 选择 stable-current、独立输出目录，省略 `--isolate-telemetry`。Eng 运行把语言位置参数改为 `eng`；沿用原本稀疏的英文覆盖。

受控叶仅为无 Godot 宿主的 StringName、日志、能量图路径，以及 Owned 的场景容器数据；不替换模型 getter、格式器或 ID 生成规则，不读取真实配置/存档，不启动 Godot、不打包。
