// ─── Incoming Damage (受击伤害计算) ──────────────────────────────

export interface IncomingDamageInput {
  vit: number;              // 体力
  spr: number;              // 精神
  biasType: 'hp' | 'dp';   // 偏向类型
  enemyBorder: number;      // 敌方边界
  enemyPerc: number;        // 敌方伤害率 %
  skillMin: number;         // 技能最小强度
  skillMax: number;         // 技能最大强度
  skillDiff: number;        // 技能差值 a
  mark: boolean;            // 属性印记 (0.1)
  necklace: boolean;        // 加防项链 (0.1)
  passiveDef: number;       // 被动加防 (%, e.g. 15 = 15%)
}

export interface IncomingDamageResult {
  biasValue: number;        // 计算出的偏向值 x
  skillPower: number;       // 技能强度 (after interpolation)
  preTaxDmg: number;        // 税前伤害 = skillPower * perc
  defMultiplier: number;    // 加防乘区 = (1-mark) * (1-necklace) * (1-passive)
  avgDmg: number;           // 均伤
  minDmg: number;           // 0.9 下限
  maxDmg: number;           // 1.1 上限
}

export function calcIncomingDamage(input: IncomingDamageInput): IncomingDamageResult {
  const { vit, spr, biasType, enemyBorder, enemyPerc, skillMin, skillMax, skillDiff, mark, necklace, passiveDef } = input;

  // 1. Bias value
  const biasValue = biasType === 'hp'
    ? (vit * 2 + spr * 1) / 3
    : (vit * 1 + spr * 2) / 3;

  // 2. Skill power interpolation
  const diff = enemyBorder - biasValue;
  let skillPower: number;
  if (diff >= skillDiff) {
    skillPower = skillMax;
  } else if (diff >= 0) {
    skillPower = skillMin + (skillMax - skillMin) * (diff / skillDiff);
  } else if (diff >= -skillDiff) {
    skillPower = skillMin + (skillMin - 1) * (diff / skillDiff);
  } else {
    skillPower = 1;
  }

  // 3. Pre-tax damage
  const preTaxDmg = skillPower * enemyPerc;

  // 4. Defense multiplier (multiply sequentially)
  let defMultiplier = 1;
  if (mark) defMultiplier *= (1 - 0.1);
  if (necklace) defMultiplier *= (1 - 0.1);
  if (passiveDef > 0) defMultiplier *= (1 - passiveDef / 100);

  // 5. Average damage
  const avgDmg = preTaxDmg * defMultiplier;

  return {
    biasValue,
    skillPower,
    preTaxDmg,
    defMultiplier,
    avgDmg,
    minDmg: avgDmg * 0.9,
    maxDmg: avgDmg * 1.1,
  };
}
