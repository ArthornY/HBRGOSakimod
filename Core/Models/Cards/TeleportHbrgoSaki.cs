using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class TeleportHbrgoSaki : CardModel
{
    private const string _swordheartPerChargeVar =
        "SwordheartPerCharge";

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/teleport_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Exhaust
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.Static(StaticHoverTip.Block),
        HoverTipFactory.FromPower<SwordheartPower>(),
        HoverTipFactory.FromPower<MagicSwordModePower>(),
        HoverTipFactory.FromPower<ChargePower>()
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.Block(100m, ValueProp.Move),
            HbrSkillVars.BlockCalculationBase(0m),
            new SwordheartBlockVar().WithMultiplier(GetSwordheartAmount),
            new IntVar(_swordheartPerChargeVar, 2m),
            new PowerVar<ChargePower>(1m),
            new PowerVar<SwordheartPower>(0m)
        };

    public override bool GainsBlock => true;

    public TeleportHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        if (CombatState == null || !CombatManager.Instance.IsInProgress ||
            (Pile?.Type is not (PileType.Hand or PileType.Play) &&
             UpgradePreviewType != CardUpgradePreviewType.Combat) ||
            Owner?.Creature.HasPower<MagicSwordModePower>() == true ||
            MagicSwordEnergyPaymentHelper.CanPayWithSwordheart(this, out _, out _, out _))
        {
            description.Add("BlockPreview", string.Empty);
            return;
        }

        LocString blockPreview = new("cards", Id.Entry + ".blockPreview");
        DynamicVars.AddTo(blockPreview);
        description.Add("BlockPreview", blockPreview);
    }

    private static decimal GetSwordheartAmount(CardModel card, Creature target) =>
        Math.Max(0m, card.Owner?.Creature.GetPower<SwordheartPower>()?.Amount ?? 0m);

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        SwordheartPower swordheart =
            Owner.Creature.GetPower<SwordheartPower>();
        decimal consumedSwordheart = Math.Max(0m, swordheart?.Amount ?? 0m);
        bool wasInMagicSwordMode =
            Owner.Creature.HasPower<MagicSwordModePower>();

        if (consumedSwordheart > 0m)
        {
            MagicSwordModePower magicSwordMode =
                Owner.Creature.GetPower<MagicSwordModePower>();
            if (magicSwordMode != null)
            {
                await magicSwordMode.SpendSwordheart(
                    choiceContext,
                    consumedSwordheart,
                    this);
            }
            else
            {
                await PowerCmd.ModifyAmount(
                    choiceContext,
                    swordheart,
                    -consumedSwordheart,
                    Owner.Creature,
                    this);
            }

            await CreatureCmd.GainBlock(
                Owner.Creature,
                consumedSwordheart * DynamicVars.Block.BaseValue,
                DynamicVars.Block.Props,
                cardPlay);

            decimal chargeAmount = wasInMagicSwordMode
                ? Math.Floor(
                    consumedSwordheart /
                    DynamicVars[_swordheartPerChargeVar].BaseValue)
                : 0m;
            if (chargeAmount > 0m)
            {
                await PowerCmd.Apply<ChargePower>(
                    choiceContext,
                    Owner.Creature,
                    chargeAmount * DynamicVars["ChargePower"].BaseValue,
                    Owner.Creature,
                    this);
            }
        }

        decimal swordheartGain = DynamicVars["SwordheartPower"].BaseValue;
        if (swordheartGain > 0m)
        {
            await PowerCmd.Apply<SwordheartPower>(
                choiceContext,
                Owner.Creature,
                swordheartGain,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeBlockByPercent(40m);
        DynamicVars["SwordheartPower"].UpgradeValueBy(1m);
    }

    private sealed class SwordheartBlockVar : CalculatedBlockVar, IHbrSkillPowerVar
    {
        internal SwordheartBlockVar() : base(ValueProp.Move) { }

        public HbrSkillPowerKind SkillPowerKind => HbrSkillPowerKind.Block;
        public decimal SkillPowerPercent => HbrSkillPower.BlockPercent(Calculate(null));

        protected override DynamicVar GetExtraVar() =>
            ((CardModel)_owner).DynamicVars.Block;

        public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode,
            Creature target, bool runGlobalHooks)
        {
            // OnPlay skips the block command when there is no Swordheart to spend.
            if (Calculate(target) <= 0m)
            {
                EnchantedValue = 0m;
                PreviewValue = 0m;
                return;
            }

            base.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
            PreviewValue = Math.Max(0m, PreviewValue);
        }
    }
}
