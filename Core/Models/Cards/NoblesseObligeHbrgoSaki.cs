using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class NoblesseObligeHbrgoSaki : ThirtyOneBGardenCard, IThirtyGMemberCard
{
    internal const int Flash = 0, ShameBrand = 1, AzureIris = 2, ThunderJudgment = 3,
        Purge = 4, Moonlight = 5, Courage = 6;
    private BigInteger _growth;
    private decimal _appliedGrowthUnits;
    protected override int FlashEffectId => Flash;
    public ThirtyGMember ThirtyGMember => ThirtyGMember.ShirakawaYuina;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(855m) };
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string CourageData
    {
        get => _growth.IsZero ? string.Empty : _growth.ToString(CultureInfo.InvariantCulture);
        private set => _growth = BigInteger.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out BigInteger amount) ? BigInteger.Max(BigInteger.Zero, amount) : BigInteger.Zero;
    }
    internal BigInteger GrowthPercent => _growth;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrCriticalRateUpPower>(),
        HoverTipFactory.FromPower<HbrCriticalDamageUpPower>()
    };
    public NoblesseObligeHbrgoSaki() : base(3, CardType.Attack, TargetType.AnyEnemy) { }
    internal void SetGrowth(BigInteger amount)
    { _growth = BigInteger.Max(BigInteger.Zero, amount); SyncDamage(); }
    internal void SyncDamage()
    {
        if (!IsMutable) return;
        // The saved counter has no gameplay cap. Decimal is only the existing
        // damage engine's projection; reaching it never truncates saved growth.
        decimal units = HbrSkillPower.DamageUnits((decimal)BigInteger.Min(_growth, new BigInteger(decimal.MaxValue)));
        DynamicVars.Damage.BaseValue = DamageAmplificationLimits.AddSaturated(DynamicVars.Damage.BaseValue,
            units - _appliedGrowthUnits);
        _appliedGrowthUnits = units;
    }
    public override Task BeforeCombatStart()
    { NoblesseObligeCombatHelper.Synchronize(this); return Task.CompletedTask; }
    protected override void AfterCloned()
    { base.AfterCloned(); NoblesseObligeCombatHelper.Track(this); }
    protected override void AfterDeserialized()
    { base.AfterDeserialized(); SyncDamage(); }
    protected override void AfterDowngraded()
    { base.AfterDowngraded(); _appliedGrowthUnits = 0m; SyncDamage(); }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        NoblesseObligeCombatHelper.Synchronize(this);
        base.AddExtraArgsToDescription(text);
        text.Add("YuinaGrowth", _growth.ToString(CultureInfo.InvariantCulture));
        text.Add("YuinaPerHit", (855 + _growth).ToString(CultureInfo.InvariantCulture));
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        NoblesseObligeCombatHelper.Synchronize(this);
        if (HasUpgradeEffect(ShameBrand))
            await HbrEffectApplication.Apply<HbrAttackUpPower>(context, Owner.Creature, 65m, Owner.Creature, this,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
        if (HasUpgradeEffect(AzureIris))
            await NoblesseObligeCombatHelper.ApplyAzureIris(context, this);
        if (play.Target?.IsAlive == true && play.Target.Side != Owner.Creature.Side &&
            ReferenceEquals(play.Target.CombatState, CombatState))
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play).Targeting(play.Target)
                .WithHitCount(1).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if (HasUpgradeEffect(Purge) && this.Capability<RollGlossaryCapability>() is { } pipeline &&
            await pipeline.RollAdditionalD100(context, this) > 50)
            await PlayerCmd.GainEnergy(1m, Owner);
    }
}
