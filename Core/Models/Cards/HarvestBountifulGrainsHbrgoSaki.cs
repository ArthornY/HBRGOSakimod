using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class HarvestBountifulGrainsHbrgoSaki : ThirtyOneBGardenCard, IThirtyGMemberCard
{
    internal const int Flash = 0, HarvestFestival = 1, Vanguard = 2, DivineCatch = 3,
        LovingRiceBall = 4, TropicalSushi = 5, Parry = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyGMember ThirtyGMember => ThirtyGMember.KuraSatomi;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(36m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<BreakPower>(), HoverTipFactory.FromPower<HbrMoralePower>(), HoverTipFactory.FromPower<HbrDefenseDownPower>() };
    public HarvestBountifulGrainsHbrgoSaki() : base(1, CardType.Attack, TargetType.AnyEnemy) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        HbrSkillCardDescription.AddDamageParameters(this, text, "SatomiFestivalDamage", HbrSkillPower.DamageUnits(133m));
    }
    protected override void OnUpgrade() { base.OnUpgrade(); KuraSatomiCombatHelper.Invalidate(Owner); }
    protected override void AfterDeserialized() { base.AfterDeserialized(); KuraSatomiCombatHelper.Invalidate(Owner); }
    protected override void AfterDowngraded() { base.AfterDowngraded(); KuraSatomiCombatHelper.Invalidate(Owner); }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Target?.IsAlive != true || play.Target.Side == Owner.Creature.Side ||
            !ReferenceEquals(play.Target.CombatState, CombatState)) return;
        using var receipt = KuraSatomiCombatHelper.Begin(this, play.Target);
        var attack = DamageCmd.Attack(KuraSatomiCombatHelper.GetAttackUnits(this, play.Target)).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitCount(KuraSatomiCombatHelper.GetHitCount(this, play.Target)).WithHitFx("vfx/vfx_attack_slash");
        receipt.Command = attack;
        await attack.Execute(context);
        if (receipt.Festival)
            await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, Owner.Creature, 100m, Owner.Creature, this,
                new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
        await KuraSatomiCombatHelper.CompleteAttack(context, receipt);
    }
}
