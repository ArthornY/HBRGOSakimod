using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class FreshDeathHbrgoSaki : ThirtyOneBGardenCard
{
    internal const int NearDeathExperiment = 0, TrickCannon = 1, SaturatedEnhancement = 2,
        SuperbSpectacle = 3, Flash = 4, DarkCheer = 5, DieTogether = 6;
    protected override int FlashEffectId => Flash;
    public override TargetType TargetType => HasUpgradeEffect(SaturatedEnhancement) ? TargetType.AllAllies : TargetType.AnyPlayer;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrCriticalRateUpPower>(),
      HoverTipFactory.FromPower<HbrCriticalDamageUpPower>(), HoverTipFactory.FromPower<HbrMoralePower>(),
      HoverTipFactory.FromPower<HbrAttackDownPower>(), HoverTipFactory.FromPower<HbrDefenseDownPower>(),
      HoverTipFactory.FromPower<FragilePower>(), HoverTipFactory.FromPower<HbrCalamityPower>() };
    public FreshDeathHbrgoSaki() : base(1, CardType.Skill, TargetType.AnyPlayer) { }
    protected override void OnUpgrade() { base.OnUpgrade(); if (IsMutable) ThirtyOneBCardCombatHelper.InvalidatePermanent(Owner); }
    protected override void AfterDowngraded() { base.AfterDowngraded(); if (IsMutable) ThirtyOneBCardCombatHelper.InvalidatePermanent(Owner); }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        Creature self = Owner.Creature;
        decimal basis = HasUpgradeEffect(NearDeathExperiment) && LargeCreatureHealthHelper.GetCurrentHp(self) <
            LargeCreatureHealthHelper.GetMaxHp(self) * .5m ? 120m : 60m;
        Creature[] recipients = HasUpgradeEffect(SaturatedEnhancement)
            ? CombatState.GetTeammatesOf(self).Where(ally => ally.IsAlive && ally.Player != null).ToArray()
            : new[] { play.Target?.Player != null ? play.Target : self };
        foreach (Creature target in recipients)
        {
            await HbrEffectApplication.Apply<HbrAttackUpPower>(context, target, basis, self, this);
            if (!HasUpgradeEffect(SuperbSpectacle)) continue;
            await ThirtyOneAMemoryUpgradeHelper.GrantCriticals(context, target, self, this);
            await ThirtyOneBCardCombatHelper.RestorePlayerDp(context, target, .05m, this);
        }
        if (HasUpgradeEffect(DarkCheer)) await PowerCmd.Apply<HbrMoralePower>(context, self, 2m, self, this);
        if (!HasUpgradeEffect(TrickCannon)) return;
        var duration = new HbrEffectProfile(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2);
        using var batch = HbrDebuffApplicationScope.BeginBatch(self);
        foreach (Creature enemy in CombatState.HittableEnemies.ToArray())
        {
            int result = Owner.RunState.Rng.Niche.NextInt(4);
            switch (result)
            {
                case 0: await HbrPercentDebuffApplication.ApplyBasis<HbrAttackDownPower>(context, enemy, 20m, self, this, profile: duration); break;
                case 1: await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy, 20m, self, this, profile: duration); break;
                case 2: await HbrPercentDebuffApplication.ApplyBasis<FragilePower>(context, enemy, 46.875m, self, this, profile: duration); break;
                case 3: await PowerCmd.Apply<HbrCalamityPower>(context, enemy, 1m, self, this); break;
            }
        }
    }
}
