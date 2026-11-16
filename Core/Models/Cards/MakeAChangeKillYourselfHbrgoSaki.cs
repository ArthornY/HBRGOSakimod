using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MakeAChangeKillYourselfHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneDMemberCard
{
    internal const int Flash = 0, RestInPeace = 1, DontDieFirst = 2, GoodnightExtinction = 3,
        Compensation = 4, GiftForYou = 5, OnePersonBand = 6;
    protected override int FlashEffectId => -1;
    public ThirtyOneDMember ThirtyOneDMember => ThirtyOneDMember.InochiFubuki;
    internal int BaseHits => 3 + (HasUpgradeEffect(OnePersonBand) ? FubukiChangeCombatHelper.GetPriorTypes(Owner) : 0);
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(40m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<HbrDefenseDownPower>(), HoverTipFactory.FromPower<HbrCalamityPower>(),
      HoverTipFactory.FromPower<HighDimensionalShellPower>() }
        .Append(HoverTipCompatibility.CreateRelicReference<HBRGOSakiMod.Core.Models.Relics.Overclock>());
    public MakeAChangeKillYourselfHbrgoSaki() : base(0, CardType.Attack, TargetType.AllEnemies) { }
    protected override void OnUpgrade()
    {
        bool before = HasUpgradeEffect(RestInPeace);
        base.OnUpgrade();
        if (!before && HasUpgradeEffect(RestInPeace)) DynamicVars.Damage.UpgradeValueBy(HbrSkillPower.DamageUnits(23m));
    }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("FubukiHits", BaseHits);
        text.Add("FubukiDpCost", HasUpgradeEffect(Flash) ? 5 : 10);
        text.Add("FubukiDefense", HbrCardFaceDescription.Percent(this, "DefenseDown", HasUpgradeEffect(DontDieFirst) ? 45m : 20m));
        text.Add("FubukiGift", ((FubukiChangeCombatHelper.GiftMultiplier(this) - 1m) * 100m).ToString("0.#", CultureInfo.InvariantCulture));
        text.Add("FubukiCurrent", string.Empty);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        FubukiChangeCombatHelper.PayBaseCost(this);
        if (HasUpgradeEffect(Compensation)) FubukiChangeCombatHelper.PayCompensation(this);
        using (var receipt = FubukiChangeCombatHelper.Begin(this))
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
                .TargetingAllOpponents(CombatState).WithHitCount(BaseHits).WithHitFx("vfx/vfx_attack_slash").Execute(context);
            await FubukiChangeCombatHelper.CompleteAttack(context, receipt);
        }
        using var batch = HbrDebuffApplicationScope.BeginBatch(Owner.Creature);
        foreach (var enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
        {
            await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy,
                HasUpgradeEffect(DontDieFirst) ? 45m : 20m, Owner.Creature, this,
                new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
            if (HasUpgradeEffect(GoodnightExtinction))
                await PowerCmd.Apply<HbrCalamityPower>(context, enemy, 20m, Owner.Creature, this);
        }
    }
}
