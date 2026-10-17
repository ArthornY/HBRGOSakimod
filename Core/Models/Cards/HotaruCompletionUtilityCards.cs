using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_THIRD_KNOCK")]
public sealed class HotaruThirdKnock : HotaruCannonCard
{
    public HotaruThirdKnock() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new IntVar("Activations", 1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruThirdKnockPower>() };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        PowerCmd.Apply<HotaruThirdKnockPower>(context, Owner.Creature, DynamicVars["Activations"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

[CustomID("HOTARU_UNTIL_DAWN")]
public sealed class HotaruUntilDawn : HotaruCannonCard
{
    public HotaruUntilDawn() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new EnergyVar(2) };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruUntilDawnPower>() };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        HotaruUntilDawnPower.Apply(context, Owner.Creature, DynamicVars.Energy.IntValue, this);
    protected override void OnUpgrade() => DynamicVars.Energy.UpgradeValueBy(1);
}
