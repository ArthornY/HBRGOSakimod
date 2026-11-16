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
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ExorcisingStringResoundHbrgoSaki : ThirtyOneBGardenCard, IThirtyGMemberCard
{
    internal const int Flash = 0, CursedArrow = 1, GrassArrow = 2, NoRather = 3,
        Weakening = 4, IceFlower = 5, Bowstring = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyGMember ThirtyGMember => ThirtyGMember.KiryuMiya;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(107m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<ExorcisingStringPower>(), HoverTipFactory.FromPower<FragilePower>(),
      HoverTipFactory.FromPower<HbrAttackDownPower>(), HoverTipFactory.FromPower<HbrDefenseDownPower>(),
      HoverTipFactory.FromPower<HbrRecoveryDownPower>() };
    public ExorcisingStringResoundHbrgoSaki() : base(1, CardType.Power, TargetType.Self) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        HbrSkillCardDescription.AddDamageParameters(this, text, "MiyaArrowDamage", HbrSkillPower.DamageUnits(144m));
    }
    protected override void OnUpgrade()
    {
        base.OnUpgrade();
        if (HasUpgradeEffect(Bowstring)) AddKeyword(CardKeyword.Innate);
        KiryuExorcisingStringCombatHelper.Invalidate(Owner);
    }
    protected override void AfterDeserialized()
    { base.AfterDeserialized(); KiryuExorcisingStringCombatHelper.Invalidate(Owner); }
    protected override void AfterDowngraded()
    { base.AfterDowngraded(); KiryuExorcisingStringCombatHelper.Invalidate(Owner); }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!HbrBattleStatsHelper.IsEnabled(Owner)) return;
        var power = (ExorcisingStringPower)ModelDb.Power<ExorcisingStringPower>().ToMutable();
        power.RegisterSource(this);
        // One independent ongoing effect per actual play, rather than a
        // dice-scaled number of layers or a merged singleton.
        await PowerCmd.Apply(context, power, Owner.Creature, 1m, Owner.Creature, null);
        if (HasUpgradeEffect(IceFlower)) await KiryuExorcisingStringCombatHelper.IceFlowerAttack(context, this);
    }
}
