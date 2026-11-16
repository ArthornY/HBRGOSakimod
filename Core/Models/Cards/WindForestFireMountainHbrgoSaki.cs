using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class WindForestFireMountainHbrgoSaki : ThirtyOneBGardenCard, IThirtyGMemberCard
{
    internal const int Flash = 0, WarMerit = 1, Wholehearted = 2, ExplosiveServe = 3,
        ImmovableMountain = 4, GentleForest = 5, SelflessDream = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyGMember ThirtyGMember => ThirtyGMember.TsukishiroMonaka;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(232m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<HbrFaithPower>(), HoverTipFactory.FromPower<HbrDefenseDownPower>(),
        HoverTipFactory.FromPower<HbrDefenseUpPower>(), HoverTipFactory.FromKeyword(CardKeyword.Retain)
    };
    public WindForestFireMountainHbrgoSaki() : base(2, CardType.Attack, TargetType.AllEnemies) { }
    protected override void OnUpgrade()
    { base.OnUpgrade(); if (HasUpgradeEffect(GentleForest)) AddKeyword(CardKeyword.Retain); }
    public override Task BeforeSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner?.Creature is { } owner && side == owner.Side && participants.Contains(owner))
            MonakaWindForestCombatHelper.EndInHand(this);
        return Task.CompletedTask;
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        using var receipt = MonakaWindForestCombatHelper.BeginPlay(this, play);
        var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .TargetingAllOpponents(CombatState).WithHitCount(1).WithHitFx("vfx/vfx_attack_slash");
        receipt.Command = attack;
        await attack.Execute(context);
        HbrFaithResource.Gain(Owner.Creature, 1);
        if (HasUpgradeEffect(WarMerit)) HbrFaithResource.Gain(Owner.Creature, receipt.HitTargets.Count);
        if (HasUpgradeEffect(ExplosiveServe))
        {
            using var batch = HbrDebuffApplicationScope.BeginBatch(Owner.Creature);
            foreach (Creature enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy, 20m, Owner.Creature, this,
                    new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
        }
        if (HasUpgradeEffect(ImmovableMountain))
            await HbrEffectApplication.Apply<HbrDefenseUpPower>(context, Owner.Creature, 30m, Owner.Creature, this,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
        if (receipt.Selfless)
        {
            HbrFaithResource.Clear(Owner.Creature);
            await MonakaFriendlyFireHelper.DamageTeam(context, this);
        }
    }
}
