using HBRGOSakiMod.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
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
	private const string DefaultPortrait = "packed/card_portraits/hbrgo_saki/strike_hbrgo_saki.png";
	private const string ThirtyAPortrait = "packed/card_portraits/hbrgo_saki/strike_30a_hbrgo_saki.png";
	private const string FistPortrait = "packed/card_portraits/hbrgo_saki/strike_31o_fist_hbrgo_saki.png";
	private const string DualSwordsPortrait = "packed/card_portraits/hbrgo_saki/strike_31o_dual_swords_hbrgo_saki.png";
	private const string BowPortrait = "packed/card_portraits/hbrgo_saki/strike_31o_bow_hbrgo_saki.png";

	internal bool TargetsAllOpponents { get; set; }

	internal enum StrikePortraitMode { Fist, Legacy, ThirtyA, DualSwords, Bow }

	internal StrikePortraitMode CurrentPortraitMode
	{
		get
		{
			if (!IsMutable || Owner == null)
			{
				return HbrgoSakiCardPortraitContext.LibraryVariant == HbrgoSakiVariant.ThirtyA
					? StrikePortraitMode.ThirtyA
					: StrikePortraitMode.Fist;
			}
			if (Owner.Character is HbrgoSaki30A)
			{
				return StrikePortraitMode.ThirtyA;
			}
			if (Owner.Character is not HbrgoSaki31O)
			{
				return StrikePortraitMode.Legacy;
			}

			var creature = Owner.Creature;
			if (creature?.CombatState == null || creature.HasPower<RaptureScythePower>())
			{
				return StrikePortraitMode.Fist;
			}
			if (creature.HasPower<ReptureBowPower>())
			{
				return StrikePortraitMode.Bow;
			}
			if (creature.HasPower<ReptureDualSwordsPower>() ||
				creature.HasPower<MagicSwordModePower>() || creature.HasPower<CrimsonBladeFlowPower>())
			{
				return StrikePortraitMode.DualSwords;
			}
			return StrikePortraitMode.Fist;
		}
	}

	public override string PortraitPath => ImageHelper.GetImagePath(CurrentPortraitMode switch
	{
		StrikePortraitMode.ThirtyA => ThirtyAPortrait,
		StrikePortraitMode.DualSwords => DualSwordsPortrait,
		StrikePortraitMode.Bow => BowPortrait,
		StrikePortraitMode.Legacy => DefaultPortrait,
		_ => FistPortrait
	});

	public override string BetaPortraitPath => PortraitPath;

	public override IEnumerable<string> AllPortraitPaths => new[]
	{
		ImageHelper.GetImagePath(DefaultPortrait),
		ImageHelper.GetImagePath(ThirtyAPortrait),
		ImageHelper.GetImagePath(FistPortrait),
		ImageHelper.GetImagePath(DualSwordsPortrait),
		ImageHelper.GetImagePath(BowPortrait)
	};

	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DamageVar[] { HbrSkillVars.Damage(100m, ValueProp.Move) };

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
				.FromCardCompat(this, cardPlay)
				.TargetingAllOpponents(Owner.Creature.CombatState)
				.WithHitFx("vfx/vfx_attack_slash")
				.SpawningHitVfxOnEachCreature()
				.Execute(choiceContext);
			return;
		}

		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
			.FromCardCompat(this, cardPlay)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeDamageByPercent(50m);
	}
}
