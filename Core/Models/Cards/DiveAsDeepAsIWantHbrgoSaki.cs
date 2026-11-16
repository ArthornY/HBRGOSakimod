using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class DiveAsDeepAsIWantHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneDMemberCard
{
    internal const int Flash = 0, WideOcean = 1, AbyssSmash = 2, SeaDuo = 3,
        JellyfishDance = 4, FishOnTheBoard = 5, RainbowMiracle = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneDMember ThirtyOneDMember => ThirtyOneDMember.MizuharaAina;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(152m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<HbrDefenseDownPower>(), HoverTipFactory.FromPower<FragilePower>(),
      HoverTipFactory.FromPower<BreakPower>(), HoverTipFactory.FromPower<DestructionRatePower>(),
      HoverTipFactory.FromPower<HbrRecoveryDownPower>() }.Append(HoverTipCompatibility.CreateRelicReference<Overclock>());
    public DiveAsDeepAsIWantHbrgoSaki() : base(10, CardType.Attack, TargetType.AllEnemies) { }
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || !IsMutable || Owner?.Creature?.CombatState == null) return false;
        modifiedCost = AinaDeepDiveCombatHelper.ModifiedCost(this, originalCost);
        return modifiedCost != originalCost;
    }
    protected override void OnUpgrade()
    {
        bool before = HasUpgradeEffect(WideOcean);
        base.OnUpgrade();
        if (!before && HasUpgradeEffect(WideOcean)) DynamicVars.Damage.UpgradeValueBy(HbrSkillPower.DamageUnits(80m));
    }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("AinaDebuffs", AinaDeepDiveCombatHelper.DebuffKinds(this));
        text.Add("AinaDestruction", HasUpgradeEffect(WideOcean) ? 1.6m : 1.2m);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool first = AinaDeepDiveCombatHelper.MarkPlayed(this);
        if (HasUpgradeEffect(SeaDuo)) Owner.GetRelic<Overclock>()?.GainLevelProgress(50m);
        if (HasUpgradeEffect(AbyssSmash))
        {
            using var batch = HbrDebuffApplicationScope.BeginBatch(Owner.Creature);
            foreach (var enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<FragilePower>(context, enemy, 42m, Owner.Creature, this,
                    new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
        }
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .TargetingAllOpponents(CombatState).WithHitCount(10).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        bool permanent = first && HasUpgradeEffect(JellyfishDance);
        decimal basis = 50m + (HasUpgradeEffect(FishOnTheBoard) && AinaDeepDiveCombatHelper.HasBrokenEnemy(this) ? 25m : 0m);
        HbrEffectProfile profile = permanent ? new(HbrEffectLimit.None, HbrEffectExit.Eternal, 1)
            : new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1);
        using (HbrDebuffApplicationScope.BeginBatch(Owner.Creature))
            foreach (var enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
            {
                await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy, basis,
                    Owner.Creature, this, profile);
                if (permanent) await HbrEffectApplication.Apply<HbrRecoveryDownPower>(context, enemy,
                    100m, Owner.Creature, this, profile: new(HbrEffectLimit.None, HbrEffectExit.Eternal, 1), potencyOverride: 100m);
            }
    }
}
