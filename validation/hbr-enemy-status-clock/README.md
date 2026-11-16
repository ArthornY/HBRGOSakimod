# 敌方状态与原生回合检查

本套件覆盖2026-11-14怪物需求1、2、4，以及2026-11-15需求七、八。它直接复用冻结的原生模型、战斗、角色及框架应用宿主，运行真实 `PowerCmd.Apply`、原生 `Hook.BeforeSideTurnStart`、`Hook.AfterEnergyReset`，以及stable的 `Hook.AfterTurnEnd`／beta的 `Hook.AfterSideTurnEnd`。`LocalContext.NetId`只提供本地身份，使原生分发不会因无本地会话而提前返回。

检查内容：

- 六只表怪攻降、防降的None／2或4敌方回合末时钟，同一目标三份以上并存；下一玩家回合的真实伤害修饰仍减伤，下一敌方攻击的真实修饰仍增伤。幻影保留默认两份及一敌方回合末时钟。
- 封印、束缚、眩晕、混乱各自的退出时钟；重复束缚与眩晕共同只扣一次[EE]，0／1点能量扣到0。
- 两份持续DP伤害各触发4次；病毒两份降低60%上限，四份最低降至0，上限恢复不返还DP；幻惑层数相加并从最早到期者扣除。玩家病毒、幻惑按PlayerTurnEnd 2、混乱按PlayerTurnEnd 3；敌方持有者仍按EnemyTurnEnd 2／2／3。
- 备用持续伤害的玩家／敌人DP先扣、生命溢出、无视格挡和真实Break；限伤保留DP时不允许生命溢出穿盾；最大HP来自精确的大数生命账本而非原生代理值。
- 能量流失等原生能量重置后才扣，各份相加、各自倒计时，并拒绝敌人及召唤物。
- 两个备用状态经过原生 `SavedProperties.From` → JSON → `SavedProperties.Fill` 往返，保存强度、退出条件、None政策，原生Amount另行恢复；行动锁的一次消费标记也是原生保存属性。

调用示例：

```powershell
./outputs/31d-and-monsters-20261115/run-suite.ps1 -Suite hbr-enemy-status-clock -Profile stable
./outputs/31d-and-monsters-20261115/run-suite.ps1 -Suite hbr-enemy-status-clock -Profile beta
```

同一个项目的两个profile顺序编译运行，避免共用obj互相覆盖。runner默认读取不可变候选manifest并校验SHA-256；最终报告在各profile的 `hbr-enemy-status-clock-report.json`。

引擎边界：只隔离终端HP写入、能量消费的UI叶子及动画等待，保留实际应用、原生钩子、DP账本、Break命令及保存系统。HP叶子检查实际原生命令收到的准确数值与Unpowered／Unblockable标记。没有Godot进程、图片导入、实际画面、声音、存档文件或多人会话。
