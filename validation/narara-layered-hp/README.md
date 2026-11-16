# 柴犬四条 HP 原生写入回归

在任何模型 ID 查找前安装实际 BaseLib PrefixIdPatch。模型、Creature 和 combat 使用原生构造器；新能力经原生 ApplyInternal 和真实 AfterApplied 安装。普通小 HP 也必须存在真实 LargeHealthPower 权威账本。LoseHpInternal、SetCurrentHpInternal、HealInternal 与生产账本写入均保留，不使用 HP 或回血终端替身。

数值断言包括奇数四等分、超过原生上限及 2^64 总 HP、逐条清空/溢出/真实 DamageResult、回血/小数/零伤害、最大生命增长、非柴犬和 allied phantom 负控及 legacy 模式。完整原生 Damage、SetCurrentHp 验证裁剪后的真实 delta；非零 Heal 验证实际恢复7 HP及其通知。原生过量回血的通知语义未改，不声称该原生通知总是实际恢复量。第四条 Damage 会真实执行死亡事件、移出战斗和能力清理。完整 force Kill 验证 2^64 账本 drain 和精确 delta，没有死亡写入替身；胜利/奖励界面不在本宿主范围内。

边界验证使用实际 production scopes，覆盖同次命中补刀、Side 改变后的短锁、裸卡外强制 EndTurn、ready 后仍挡、原生额外回合、两人联机额外回合/同伴死亡和 nested failure 被 outer 捕获后的释放。个人/OD 使用受控 session 数据但调用真实 coordinator token getter，验证 identity 恢复；不声称执行了完整网络插入回合。

新能力使用真实 CustomID 与中英文 LocString；SavedProperties 覆盖精确 HP 账本、初始分条基准和已消耗容量字符串，剩余条数另验证原生 mutable clone。Amount 不是 SavedProperties 字段，本套件没有执行 PowerState 数据包。不能把模型字段往返称为完整中途战斗存读档或网络验证。

快照验证手工驱动真实 production BeginCapture/CaptureCurrentHp/CaptureMaxHp/CapturePowerAmount/FinishCapture，原生行容器为受控工厂；实际 extension PacketWriter/PacketReader 序列化、反序列化和原生 Anonymized 的生产 postfix 均执行。不称为完整原生 FromRun、完整 native snapshot packet 或 transport 测试，也不称为实体恢复。覆盖两字段分别改变冻结 hash、冻结后不追随活体变化、全部/部分空字段 readonly fallback、detached pet AddExtra、spent 大于初始容量和无新 marker 的旧 JSON 字节兼容。旧 golden 来自 F757 实际 helper 的 --probe-legacy-snapshot，SHA 为 89BC6443B1E40F7B9BDFB9AA8741887AAECE0F51443900E8480BD41CBCED2201。

最大生命增长遵循当前能力策略：新增容量仅进入当前条，不赠送实际 HP、不复活已经消耗的条。初始100 HP 的四条仍以75/50/25为储备边界；最大生命增至200后，第一条容量125，清空后余75，其余条恢复原25容量。已消耗第一条25时增至200，当前条最多恢复到175。

```powershell
$repo='D:/stsmod/HBRGOSakiMod'
$stage='D:/stsmod/outputs/narara-four-hp-20261116'
$deps='D:/stsmod/outputs/card-inspect-aoi-20261116/profiles/beta-current'
$candidate="$stage/candidates/stable/HBRGOSakiMod.dll"
$sha=(Get-FileHash -LiteralPath $candidate).Hash
$project="$stage/staged-files/validation/narara-layered-hp/Checks.csproj"
dotnet build $project --nologo -warnaserror -p:RepositoryRoot=$repo -p:ModAssemblyPath=$candidate -p:NativeAssemblyPath="$deps/sts2.dll" -p:BaseLibAssemblyPath="$deps/BaseLib.dll" -p:NativeRitsuAssemblyPath="$deps/STS2-RitsuLib.dll" -p:GodotSharpAssemblyPath="$deps/GodotSharp.dll" -p:NuGetAudit=false -p:OutputPath="$stage/test-bin/beta/"
dotnet "$stage/test-bin/beta/Checks.dll" $candidate $sha $deps $repo "$stage/native-beta.json" --isolate-telemetry
# stage 候选配套本地化尚未整合时，追加：
# --localization-root "$stage/staged-files/HBRGOSakiMod/localization"
```

Stable 使用 stable-current 和独立输出目录，省略 --isolate-telemetry。工程整合后可把 project 改为 "$repo/validation/narara-layered-hp/Checks.csproj"，其余显式参数不变。旧 DLL 的 --expect-old-failure 要求程序集确实没有新能力，且真正观察到 101 HP 被一击全清，不能将构造/加载异常当作预期失败。--probe-max-growth 和 --probe-legacy-snapshot 仅执行对应诊断，报告 selection 明确标记为 probe；正式验证不传这些参数。Godot/日志/等待/VFX/音效/能量图路径/本地 UI 身份与钩子演员枚举为宿主受控叶；真实 HP、回血和死亡清理逻辑不替换。
