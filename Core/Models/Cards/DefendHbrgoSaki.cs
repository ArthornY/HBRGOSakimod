using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class DefendHbrgoSaki : CardModel
{
	public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/defend_hbrgo_saki.png");

	public override string BetaPortraitPath => PortraitPath;

	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
		new IHoverTip[] { HoverTipFactory.Static(StaticHoverTip.Block) };

	protected override IEnumerable<DynamicVar> CanonicalVars =>
		new DynamicVar[] { new BlockVar(5m, ValueProp.Move) };

	public override bool GainsBlock => true;

	public DefendHbrgoSaki()
		: base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(
			Owner.Creature,
			DynamicVars.Block,
			cardPlay);
	}

	protected override void OnUpgrade()
	{
		DynamicVars.Block.UpgradeValueBy(3m);
	}
}
