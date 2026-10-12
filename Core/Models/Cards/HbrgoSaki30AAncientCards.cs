using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class DistantPrayerHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/unfettered_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        IsMutable && Owner != null &&
        ThirtyARosterSelection.CanReachMinimumLevelTotal(
            Owner.GetRelic<MomentOfReign>()?.GetConsumableRosterEntries(),
            DynamicVars["RequiredLevels"].IntValue);

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("RequiredLevels", 3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public DistantPrayerHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Ancient, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (!IsMutable || Owner?.GetRelic<MomentOfReign>() is not { } relic)
        {
            return;
        }

        int requiredLevels = DynamicVars["RequiredLevels"].IntValue;
        IReadOnlyList<int> selectedIds =
            await ThirtyARosterSelection.SelectAtLeastLevelTotal(
                choiceContext, this, relic.GetConsumableRosterEntries(), requiredLevels);
        if (selectedIds.Count == 0 || selectedIds.Distinct().Count() != selectedIds.Count)
        {
            return;
        }

        PhantomRosterEntry[] selectedEntries = relic.GetConsumableRosterEntries()
            .Where(entry => selectedIds.Contains(entry.InstanceId))
            .ToArray();
        if (selectedEntries.Length != selectedIds.Count ||
            !ThirtyARosterSelection.CanReachMinimumLevelTotal(selectedEntries, requiredLevels) ||
            !await relic.ConsumeRosterEntries(choiceContext, selectedIds) ||
            !Owner.Creature.IsAlive || !CombatManager.Instance.IsInProgress ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        CardPile drawPile = PileType.Draw.GetPile(Owner);
        if (drawPile.Cards.Count > 0)
        {
            CardModel selected =
                (await CardSelectCmd.FromCombatPile(
                    choiceContext,
                    drawPile,
                    Owner,
                    new CardSelectorPrefs(
                        new LocString("cards", Id.Entry + ".drawPileSelectionScreenPrompt"),
                        1)))
                .FirstOrDefault();
            if (selected != null && selected.Owner == Owner && selected.Pile == drawPile)
            {
                await CardPileCmd.Add(selected, PileType.Hand);
            }
        }

        await relic.RevivePhantomNow();
    }

    // MomentOfReign calls this only after confirming an owned main phantom's final death.
    internal static async Task ReturnToHandAfterPhantomDeath(Player owner)
    {
        if (owner?.PlayerCombatState == null || owner.Creature?.IsAlive != true ||
            !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        ICombatState combatState = owner.Creature.CombatState;
        CardPile drawPile = owner.PlayerCombatState.DrawPile;
        DistantPrayerHbrgoSaki[] cards = drawPile.Cards
            .OfType<DistantPrayerHbrgoSaki>()
            .Distinct()
            .ToArray();
        foreach (DistantPrayerHbrgoSaki card in cards)
        {
            if (!owner.Creature.IsAlive || owner.Creature.CombatState != combatState ||
                !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding)
            {
                return;
            }

            if (!card.IsMutable || card.Owner != owner || card.HasBeenRemovedFromState ||
                card.Pile != drawPile)
            {
                continue;
            }

            await CardPileCmd.Add(card, PileType.Hand);
        }
    }

    public override async Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel clonedBy)
    {
        if (!ReferenceEquals(card, this) ||
            card.Pile?.Type != PileType.Deck ||
            oldPileType == PileType.Deck)
        {
            return;
        }

        StrengthAmplificationHbrgoSaki starter = Owner.Deck.Cards
            .OfType<StrengthAmplificationHbrgoSaki>()
            .FirstOrDefault();
        if (starter != null)
        {
            await CardPileCmd.RemoveFromDeck(
                starter,
                showPreview: false);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["RequiredLevels"].UpgradeValueBy(-1m);
    }
}

public sealed class EcstaticCorridorHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/ecstatic_corridor_hbrgo_saki.tres");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<EcstaticCorridorPower>(1m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<EcstaticCorridorPower>()
        };

    public EcstaticCorridorHbrgoSaki()
        : base(1, CardType.Power, CardRarity.Ancient, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<EcstaticCorridorPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["EcstaticCorridorPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
