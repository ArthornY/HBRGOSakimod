using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class StrikeHbrgoSaki : CardModel
{
	public override string PortraitPath => MissingPortraitPath;

	public override string BetaPortraitPath => MissingPortraitPath;

	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DamageVar[] { new DamageVar(4m, ValueProp.Move) };

	public StrikeHbrgoSaki()
		: base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(1m);
	}
}
