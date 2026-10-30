# 正面能力：伤害增幅与连击

## 伤害增幅

「伤害增幅」与原能力「超越自我」使用同一个百分比池，每点提供 1% 伤害增幅，保留小数精度。不同来源的增幅相加，然后进入现有伤害计算流程。

- 卡牌「超越自我」名称保留，继承的幻影增伤加入「伤害增幅」。
- Boost Up、Hello world 的相关升级效果使用同一能力。
- 旧能力 ID 和旧 `DamagePercentData` 字段仍可读取；再次施加时并入已有实例，避免增加第二个能力图标或重复结算。
- 仅合并这两个能力。具有独立到期规则的回合增伤仍保留自己的生命周期。

## 连击

| 能力 | 每次攻击的效果 | 每次获得的层数／可生效次数 |
| --- | --- | --- |
| 连击（大） | 段数 ×2 | 1 |
| 连击（中） | 段数 +3 | 3 |
| 连击（小） | 段数 +1 | 5 |

同种能力的层数表示剩余次数，不会在一次攻击中叠加多次效果。每次完整攻击有实际命中后，各生效能力分别减少 1 层；一次多段或群体攻击只扣一次。格挡抵消全部伤害也算命中；无命中、预览、无增益伤害、幻影的攻击转格挡及浮游炮的反应伤害不扣层。

多种连击先加段数，再翻倍。因此原本 1 段、同时具有三种连击时为 `(1 + 3 + 1) × 2 = 10` 段。效果属于实际持有者，角色、幻影和浮游炮分别计算。浮游炮每个攻击阶段算一次攻击；只有成长或格挡的阶段不消耗次数。

复用既有攻击段数与 AttackContext 流程，幻影与浮游炮直接攻击在整次攻击外预留、结束时消费，不增加逐帧检查。取消或异常退出释放预留，避免锁住后续攻击。

刚射、刀舞、龙箭等原先逐段建立攻击命令的牌改为完整多段攻击。穿透射击保留各目标独立的基础伤害，连续斩击保留逐段重算伤害；异值追击与格挡转伤害保留各段的计算上下文。整次出牌不会因这些内部拆分多扣连击层数。

### 后续效果的调用约定

```csharp
await HbrComboPower.Grant<HbrComboLargePower>(context, target, source, card);
await HbrComboPower.Grant<HbrComboMediumPower>(context, target, source, card);
await HbrComboPower.Grant<HbrComboSmallPower>(context, target, source, card);
```

以上分别给予 1、3、5 层；重复获得累加剩余次数。原生 `PowerCmd.Apply` 和 `ModifyAmount` 的数值仍表示层数，因此翻倍等已有计数操作不会再次乘以持续次数。

新增能力尚未给现有卡牌、遗物增加获取来源；后续可按设计接入对应来源。

## 验证

稳定版与 beta 依赖均源码编译通过，0 警告、0 错误。同一稳定版构建 DLL 在两个运行时配置分别通过 158 项连击检查和 107 项增伤合并检查。

- 候选 SHA-256：`1AFC9B1BA3956A6D72898AC076DEA47216F9EF649DBA753D38EFE6BA10AB8EDD`。
- 连击检查：`validation/hbr-combo/`；报告 `outputs/hbr-combo/validation/{stable,beta}/report.json`。
- 增伤合并检查：`validation/hbr-merged-damage-buff/`；报告 `outputs/hbr-combo/merge-validation/{stable,beta}/report.json`。
- 检查覆盖真实能力 Hook、Apply/ModifyAmount、AttackContext、幻影攻击循环、代表性卡牌 OnPlay 与原生 Execute，以及小数数据、旧 ID 和快照兼容。

这是受控的托管代码验证，未启动游戏或检验 Godot 界面，也未打包或上传。
