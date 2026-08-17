using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class StrikeHbrgoSaki : CardModel
{
	internal bool TargetsAllOpponents { get; set; }

	public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/strike_hbrgo_saki.png");

	public override string BetaPortraitPath => PortraitPath;

	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DamageVar[] { new DamageVar(6m, ValueProp.Move) };

	public StrikeHbrgoSaki()
		: base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
		if (TargetsAllOpponents)
		{
			await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
				.FromCard(this)
				.TargetingAllOpponents(Owner.Creature.CombatState)
				.WithHitFx("vfx/vfx_attack_slash")
				.SpawningHitVfxOnEachCreature()
				.Execute(choiceContext);
			return;
		}

		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
			.FromCard(this)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
