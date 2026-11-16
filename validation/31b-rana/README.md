# 乐奈、白虎源码行为验证

覆盖需求 36、37。程序链接现有托管战斗 fixture，加载指定候选 DLL 和经过哈希核对的 native runtime profile。

- 实际执行两张卡的 OnPlay 和原生 AttackCommand：逐发随机选目标、攻击段数、属性/增益伤害入口。
- 猫爪印通过真实 native Damage 入口前缀修改既有独立脆弱，验证每张牌每个目标仅一次、增加 10 个百分点、保留时长。
- 白枪执行实际 DP 回复，包括 30A 当前幻影路由；战斗内重复打出只回复一次。
- 即兴/白枪通过永久来源与真实 native MutableClone/DeckVersion 链验证：未打出前进入个人额外回合也保留卡面选择，白枪一次限制跨额外回合共享，下一场正确重置。
- 即兴在战斗开始选择并在后续出牌中保留；野猫生成普通、精英及 Boss 的原生 CardReward，重复调用不重复添加。
- 野猫奖励保留持有者和三张候选。奖励兼容回归必须额外用 stable 编译的候选加载 beta 运行：只分别编译并加载同档，无法检出 C# 固化的旧 optional 构造参数。
- 原始大成功经过正常 RollGlossary 和最强之盾防御骰的真实注册/待处理入口；低原始出目的绝对大成功不触发；被抑制的大成功不触发。自动批处理覆盖掷骰瞬间手牌快照、延后抽到的牌不加入、已离手的牌跳过，以及重入队列。
- 从现有图书馆实际剧情页读取白虎相遇标记，经真实 RecordEncounters 和 GetChoices 验证正常解锁。
- 白虎验证野兽牢笼三部分、纯真的第二份自减益、破坏女王、士气、单敌追加段数、开局自打、全部七条升级。

边界：native 伤害终端 HP 写入、视觉/历史记录和 native AutoPlay 的外层出牌显示/包装被隔离；AutoPlay 内实际卡牌攻击体仍执行。不代表游戏画面、完整联机或存档往返测试。不运行 Godot、不导出、不打包。

输出报告位于 `outputs/31a-28-37-20261114/{stable,beta}/rana-byakko-report.json`；结果只适用于报告记录的 DLL SHA256。该独立项目链接旧 Effect fixture，会有该 fixture 已存在的 nullable 警告；生产模组编译单独按零警告要求验证。

编译时指定 `ModAssemblyPath`、`NativeAssemblyPath`、`NativeRitsuAssemblyPath`。Beta 另设 `BetaNativeChecks=true`。入口参数顺序：候选 DLL、候选 SHA256、native profile 目录、GodotSharp.dll 路径、报告路径。已隔离遥测的 `beta-0.113.0-test` profile 无需再传 `--isolate-telemetry`。
