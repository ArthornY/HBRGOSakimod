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

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class YourCombatDeliveryIsHereHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneEMemberCard
{
    internal const int Flash = 0, Hurricane = 1, DancingSpirit = 2, FirstRedLeaf = 3,
        Ollie = 4, Newspaper = 5, MoodMaker = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneEMember ThirtyOneEMember => ThirtyOneEMember.OshimaMinori;
    public override TargetType TargetType => HasUpgradeEffect(Hurricane) ? TargetType.AllEnemies : TargetType.AnyEnemy;
    internal int BaseHits => HasUpgradeEffect(Hurricane) ? 1 : 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(55m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<HighDimensionalShellPower>(), HoverTipFactory.FromPower<MindsEyePower>(),
      HoverTipFactory.FromPower<HbrCriticalRateUpPower>(), HoverTipFactory.FromPower<HbrCriticalDamageUpPower>(),
      HoverTipFactory.FromPower<HbrMoralePower>(), HoverTipFactory.FromPower<MinoriNextTurnEnergyDebtPower>() }
        .Append(HoverTipCompatibility.CreateRelicReference<Overclock>());
    public YourCombatDeliveryIsHereHbrgoSaki() : base(1, CardType.Attack, TargetType.AnyEnemy) { }
    protected override void OnUpgrade()
    {
        bool before = HasUpgradeEffect(Hurricane);
        base.OnUpgrade();
        if (!before && HasUpgradeEffect(Hurricane)) DynamicVars.Damage.UpgradeValueBy(HbrSkillPower.DamageUnits(341m));
    }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("MinoriTarget", HasUpgradeEffect(Hurricane) ? "所有敌人" : "目标");
        text.Add("MinoriHits", BaseHits);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool first = MinoriDeliveryCombatHelper.TryFirstInTurn(this);
        if (first && HasUpgradeEffect(DancingSpirit))
            foreach (var ally in HbrPlayerTeamTargets.Get(Owner.Creature))
                await HbrEffectApplication.ApplyMindsEye(context, ally, Owner.Creature, this);
        if (HasUpgradeEffect(FirstRedLeaf))
        {
            Owner.GetRelic<Overclock>()?.LoseLevelProgress(50m);
            foreach (var ally in HbrPlayerTeamTargets.Get(Owner.Creature))
            {
                var once = new HbrEffectProfile(HbrEffectLimit.Only, HbrEffectExit.Count, 1);
                await HbrEffectApplication.Apply<HbrCriticalRateUpPower>(context, ally, 40m, Owner.Creature, this, profile: once);
                await HbrEffectApplication.Apply<HbrCriticalDamageUpPower>(context, ally, 50m, Owner.Creature, this, profile: once);
            }
        }
        if (first && HasUpgradeEffect(MoodMaker))
            await ThirtyOneDBondHelper.GrantTeamMorale(context, Owner.Creature, 1m);
        var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .WithHitCount(BaseHits).WithHitFx("vfx/vfx_attack_slash");
        if (HasUpgradeEffect(Hurricane)) await attack.TargetingAllOpponents(CombatState).Execute(context);
        else if (play.Target?.IsAlive == true && play.Target.Side != Owner.Creature.Side &&
            ReferenceEquals(play.Target.CombatState, CombatState)) await attack.Targeting(play.Target).Execute(context);
        if (HasUpgradeEffect(Hurricane))
            await PowerCmd.Apply<MinoriNextTurnEnergyDebtPower>(context, Owner.Creature, 1m, null, null);
        if (HasUpgradeEffect(Newspaper)) await CardPileCmd.Draw(context, 1m, Owner);
    }
}
