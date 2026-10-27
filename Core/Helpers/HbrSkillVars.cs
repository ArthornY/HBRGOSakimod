using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal enum HbrSkillPowerKind { Damage, Block, DamageAndBlock }

// Metadata is a live view over native values. Upgrades, dice and card abilities
// continue to read/write the same legacy units, without a second value ledger.
internal interface IHbrSkillPowerVar
{
    HbrSkillPowerKind SkillPowerKind { get; }
    decimal SkillPowerPercent { get; }
}

internal static class HbrSkillVars
{
    internal static DamageVar Damage(decimal percent, ValueProp props = ValueProp.Move,
        decimal divisor = 1m, string name = "Damage") =>
        new SkillDamageVar(name, HbrSkillPower.DamageUnits(percent, divisor), props);

    internal static BlockVar Block(decimal percent, ValueProp props = ValueProp.Move,
        decimal divisor = 1m, string name = "Block") =>
        new SkillBlockVar(name, HbrSkillPower.BlockUnits(percent, divisor), props);

    internal static IntVar DamageBonus(string name, decimal percent, decimal divisor = 1m) =>
        new SkillBonusVar(name, HbrSkillPower.DamageUnits(percent, divisor), HbrSkillPowerKind.Damage);

    internal static IntVar BlockBonus(string name, decimal percent, decimal divisor = 1m) =>
        new SkillBonusVar(name, HbrSkillPower.BlockUnits(percent, divisor), HbrSkillPowerKind.Block);

    internal static PowerVar<T> BlockPower<T>(decimal percent, decimal divisor = 1m) where T : PowerModel =>
        new SkillBlockPowerVar<T>(HbrSkillPower.BlockUnits(percent, divisor));

    // Existing cannon growth adds one shared native amount to both ledgers.
    // Express its configuration against damage 100%; presentation can also
    // derive block percent from BaseValue without changing that shared amount.
    internal static IntVar CannonGrowthBonus(string name, decimal damagePercent, decimal divisor = 1m) =>
        new SkillBonusVar(name, HbrSkillPower.DamageUnits(damagePercent, divisor), HbrSkillPowerKind.DamageAndBlock);

    internal static CalculationBaseVar DamageCalculationBase(decimal percent, decimal divisor = 1m) =>
        new SkillCalculationBaseVar(HbrSkillPower.DamageUnits(percent, divisor), HbrSkillPowerKind.Damage);

    internal static CalculationBaseVar BlockCalculationBase(decimal percent, decimal divisor = 1m) =>
        new SkillCalculationBaseVar(HbrSkillPower.BlockUnits(percent, divisor), HbrSkillPowerKind.Block);

    internal static ExtraDamageVar ExtraDamage(decimal percent, decimal divisor = 1m) =>
        new SkillExtraDamageVar(HbrSkillPower.DamageUnits(percent, divisor));

    internal static CalculatedDamageVar CalculatedDamage(ValueProp props = ValueProp.Move) =>
        new SkillCalculatedDamageVar(props);

    internal static void UpgradeDamageByPercent(this DynamicVar variable, decimal percent, decimal divisor = 1m) =>
        variable.UpgradeValueBy(HbrSkillPower.DamageUnits(percent, divisor));

    internal static void UpgradeBlockByPercent(this DynamicVar variable, decimal percent, decimal divisor = 1m) =>
        variable.UpgradeValueBy(HbrSkillPower.BlockUnits(percent, divisor));

    internal static void UpgradeCannonGrowthByPercent(this DynamicVar variable, decimal damagePercent, decimal divisor = 1m) =>
        variable.UpgradeValueBy(HbrSkillPower.DamageUnits(damagePercent, divisor));

    private static decimal Percent(decimal units, HbrSkillPowerKind kind) =>
        kind == HbrSkillPowerKind.Block ? HbrSkillPower.BlockPercent(units) : HbrSkillPower.DamagePercent(units);

    // Native DynamicVar.Clone uses MemberwiseClone and ResetToBase, preserving
    // these subclasses and immutable kind fields in deck/combat copies.
    private sealed class SkillDamageVar(string name, decimal units, ValueProp props)
        : DamageVar(name, units, props), IHbrSkillPowerVar
    {
        public HbrSkillPowerKind SkillPowerKind => HbrSkillPowerKind.Damage;
        public decimal SkillPowerPercent => HbrSkillPower.DamagePercent(BaseValue);
    }

    private sealed class SkillBlockVar(string name, decimal units, ValueProp props)
        : BlockVar(name, units, props), IHbrSkillPowerVar
    {
        public HbrSkillPowerKind SkillPowerKind => HbrSkillPowerKind.Block;
        public decimal SkillPowerPercent => HbrSkillPower.BlockPercent(BaseValue);
    }

    private sealed class SkillBlockPowerVar<T>(decimal units) : PowerVar<T>(units), IHbrSkillPowerVar
        where T : PowerModel
    {
        public HbrSkillPowerKind SkillPowerKind => HbrSkillPowerKind.Block;
        public decimal SkillPowerPercent => HbrSkillPower.BlockPercent(BaseValue);
    }

    private sealed class SkillBonusVar(string name, decimal units, HbrSkillPowerKind kind)
        : IntVar(name, units), IHbrSkillPowerVar
    {
        public HbrSkillPowerKind SkillPowerKind => kind;
        public decimal SkillPowerPercent => Percent(BaseValue, kind);
    }

    private sealed class SkillCalculationBaseVar(decimal units, HbrSkillPowerKind kind)
        : CalculationBaseVar(units), IHbrSkillPowerVar
    {
        public HbrSkillPowerKind SkillPowerKind => kind;
        public decimal SkillPowerPercent => Percent(BaseValue, kind);
    }

    private sealed class SkillExtraDamageVar(decimal units) : ExtraDamageVar(units), IHbrSkillPowerVar
    {
        public HbrSkillPowerKind SkillPowerKind => HbrSkillPowerKind.Damage;
        public decimal SkillPowerPercent => HbrSkillPower.DamagePercent(BaseValue);
    }

    private sealed class SkillCalculatedDamageVar(ValueProp props) : CalculatedDamageVar(props), IHbrSkillPowerVar
    {
        public HbrSkillPowerKind SkillPowerKind => HbrSkillPowerKind.Damage;
        public decimal SkillPowerPercent => HbrSkillPower.DamagePercent(Calculate(null));
    }
}
