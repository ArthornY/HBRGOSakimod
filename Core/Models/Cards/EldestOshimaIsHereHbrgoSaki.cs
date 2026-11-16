using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class EldestOshimaIsHereHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneEMemberCard
{
    internal const int LovingWave = 0, InfiniteHalo = 1, Frugal = 2, LightSignpost = 3,
        LoyalOath = 4, MilitaryPay = 5, DeterminedNight = 6;
    protected override int FlashEffectId => -1;
    public ThirtyOneEMember ThirtyOneEMember => ThirtyOneEMember.OshimaIchiko;
    public EldestOshimaIsHereHbrgoSaki() : base(0, CardType.Skill, TargetType.Self) { }
    protected override void OnUpgrade() { base.OnUpgrade(); IchikoEldestCombatHelper.Invalidate(Owner); }
    protected override void AfterDowngraded() { base.AfterDowngraded(); IchikoEldestCombatHelper.Invalidate(Owner); }
    protected override void AfterDeserialized() { base.AfterDeserialized(); IchikoEldestCombatHelper.Invalidate(Owner); }
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<HbrDefenseUpPower>(), HoverTipFactory.FromPower<HbrAttackUpPower>(),
      HoverTipFactory.FromPower<HbrDefenseDownPower>(), HoverTipFactory.FromPower<FragilePower>(),
      HoverTipFactory.FromPower<IchikoVitalityUpPower>() }.Append(HoverTipCompatibility.CreateRelicReference<Overclock>());
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!HbrBattleStatsHelper.IsEnabled(Owner)) return;
        bool first = IchikoEldestCombatHelper.MarkPlayed(this);
        Creature[] allies = HasUpgradeEffect(LovingWave) ? HbrPlayerTeamTargets.Get(Owner.Creature) : new[] { Owner.Creature };
        foreach (Creature ally in allies)
        {
            await HbrEffectApplication.Apply<HbrDefenseUpPower>(context, ally, 30m, Owner.Creature, this,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, HasUpgradeEffect(LovingWave) ? 2 : 1));
            if (HasUpgradeEffect(LovingWave))
                await HbrEffectApplication.Apply<HbrAttackUpPower>(context, ally, 65m, Owner.Creature, this,
                    profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
        }
        if (first && HasUpgradeEffect(InfiniteHalo))
        {
            using var batch = HbrDebuffApplicationScope.BeginBatch(Owner.Creature);
            foreach (Creature enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy, 20m, Owner.Creature, this,
                    new(HbrEffectLimit.None, HbrEffectExit.Eternal, 1));
            Owner.GetRelic<Overclock>()?.GainLevelProgress(50m);
        }
        if (HasUpgradeEffect(LoyalOath))
        {
            using var batch = HbrDebuffApplicationScope.BeginBatch(Owner.Creature);
            foreach (Creature enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<FragilePower>(context, enemy, 42m, Owner.Creature, this,
                    new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2));
        }
        if (HasUpgradeEffect(DeterminedNight) && HbrBattleStatsHelper.TryGetStats(Owner.Creature, out var stats))
            await HbrEffectApplication.Apply<IchikoVitalityUpPower>(context, Owner.Creature,
                stats.Vitality * .15m, Owner.Creature, this, profile: new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2),
                potencyOverride: stats.Vitality * .15m);
        await IchikoEldestCombatHelper.CallName(context, this);
        if (HasUpgradeEffect(LightSignpost))
        {
            Owner.GetRelic<Overclock>()?.LoseLevelProgress(50m);
            await PlayerCmd.GainEnergy(2m, Owner);
        }
    }
}
