# HBR敌人追加机制验证

针对2026-11-15怪物需求10–18的独立托管宿主；复用hbr-act-rosters中的原生实体／百分比效果底座，增加真实Monster.PerformMove、原生回合Hook、原生AfterDeath监听和SavedProperties roundtrip。原表行、权重、一次标记仍由rosters套件逐行核对。

覆盖三柱目标OD倍率与即时替换、Barrel与钻石眼魔12回合调度／暂停游标／无额外AI随机、DP击破取消和分档、异蟹挑衅原生目标校验与狂暴同桶、猫角兽群体增益和死亡即时刷新、Gate三变种倒数与多门扣能量去重、象牙异蛛持续伤害、深渊S灾厄、核心能力驱散政策。全部新增机制核验旧模式／幻影隔离。

动画、等待、Godot预览与战斗历史输出叶被隔离；攻击执行叶记录原始招式伤害／段数／范围，三柱和狂暴倍率另外走原生ModifyDamage钩子。它不是渲染界面、完整存档或双端联机实测。

通过outputs/31d-and-monsters-20261115/run-suite.ps1 -Suite hbr-enemy-flavor -Profile stable（然后beta）运行；同工程两profile顺序执行。报告、编译日志和运行日志均输出到该阶段对应profile，报告校验不可变候选DLL与原生依赖清单。
