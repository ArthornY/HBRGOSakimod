using HBRGOSakiMod.Core.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
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
	private const string DefaultPortrait = "packed/card_portraits/hbrgo_saki/defend_hbrgo_saki.png";
	private const string ShieldPortrait = "packed/card_portraits/hbrgo_saki/defend_30a_31o_hbrgo_saki.png";

	private bool UsesShieldPortrait => IsMutable && Owner != null
		? Owner.Character is HbrgoSaki30A or HbrgoSaki31O
		: HbrgoSakiCardPortraitContext.LibraryVariant.HasValue;

	public override string PortraitPath => ImageHelper.GetImagePath(
		UsesShieldPortrait
			? ShieldPortrait
			: DefaultPortrait);

	public override string BetaPortraitPath => PortraitPath;

	public override IEnumerable<string> AllPortraitPaths => new[]
	{
		ImageHelper.GetImagePath(DefaultPortrait),
		ImageHelper.GetImagePath(ShieldPortrait)
	};

	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
		new IHoverTip[] { HoverTipFactory.Static(StaticHoverTip.Block) };

	protected override IEnumerable<DynamicVar> CanonicalVars =>
		new DynamicVar[] { HbrSkillVars.Block(100m, ValueProp.Move) };

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
		DynamicVars.Block.UpgradeBlockByPercent(60m);
	}
}
