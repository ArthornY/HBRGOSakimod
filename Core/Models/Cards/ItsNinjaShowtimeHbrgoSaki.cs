using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ItsNinjaShowtimeHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneCMemberCard
{
    internal const int Flash = 0, WillActivation = 1, AnnihilatingShadowClone = 2, IceCrystal = 3,
        WildRoar = 4, BeastFangCrush = 5, FaintPresence = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneCMember ThirtyOneCMember => ThirtyOneCMember.KanzakiAdelheid;
    internal int BaseHitCount => HasUpgradeEffect(AnnihilatingShadowClone) ? 6 : 3;
    internal decimal DestructionCoefficient => HasUpgradeEffect(AnnihilatingShadowClone) ? 16.8m : 4.8m;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(50m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<DestructionRatePower>(), HoverTipFactory.FromPower<BreakPower>(),
      HoverTipFactory.FromPower<HbrComboLargePower>(), HoverTipFactory.FromPower<HbrAttackDownPower>(),
      HoverTipFactory.FromKeyword(CardKeyword.Retain) }.Append(HoverTipCompatibility.CreateRelicReference<Overclock>());
    public ItsNinjaShowtimeHbrgoSaki() : base(1, CardType.Attack, TargetType.AllEnemies) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("KanzakiHits", BaseHitCount);
        text.Add("KanzakiDestruction", DestructionCoefficient);
        decimal retained = IsMutable ? KanzakiShowtimeCombatHelper.GetStoredAmplification(this) : 0m;
        text.Add("KanzakiRetained", retained);
        text.Add("KanzakiCurrent", string.Empty);
    }
    protected override void OnUpgrade()
    {
        base.OnUpgrade();
        if (HasUpgradeEffect(FaintPresence)) AddKeyword(CardKeyword.Retain);
    }
    public override Task BeforeSideTurnEndEarly(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner?.Creature.Side && participants.Contains(Owner.Creature))
            KanzakiShowtimeCombatHelper.RecordRetained(this, OverclockImmediateTurnCoordinator.GetTurnToken(Owner));
        return Task.CompletedTask;
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        using var receipt = KanzakiShowtimeCombatHelper.BeginPlay(this);
        bool roar = HasUpgradeEffect(WildRoar) && KanzakiShowtimeCombatHelper.TryUseRoar(this);
        if (HasUpgradeEffect(WillActivation))
            await HbrComboPower.Grant<HbrComboLargePower>(context, Owner.Creature, Owner.Creature, this);
        if (HasUpgradeEffect(IceCrystal))
        {
            var profile = new HbrEffectProfile(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1);
            using var batch = HbrDebuffApplicationScope.BeginBatch(Owner.Creature);
            foreach (var enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<HbrAttackDownPower>(context, enemy, 20m, Owner.Creature, this, profile);
        }
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .TargetingAllOpponents(CombatState).WithHitCount(BaseHitCount).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if (roar) PersonalImmediateTurnCoordinator.Request(Owner, 1);
    }
}
