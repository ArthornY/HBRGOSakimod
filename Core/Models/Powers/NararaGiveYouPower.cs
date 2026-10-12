using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NararaGiveYouPower : PowerModel, ICustomPower
{
    internal const int CardsPerCopy = 7;

    private Dictionary<Player, int> _cardsPlayedByPlayer = new();
    private HashSet<CardPlay> _pendingCopies = new();

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount
    {
        get
        {
            if (!IsMutable || Owner?.CombatState is not { } combat) return CardsPerCopy;
            Player player = LocalContext.GetMe(combat.Players) ??
                (combat.Players.Count == 1 ? combat.Players[0] : null);
            return GetCardsUntilTrigger(player);
        }
    }

    public override LocString Description =>
        HbrgoSakiEnergyIconHelper.Add31OPrefix(base.Description);

    public string CustomPackedIconPath => "res://images/powers/narara_give_you_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[] { HoverTipFactory.FromKeyword(CardKeyword.Exhaust) };

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // Match the sleeping ability: only manual plays advance the counter.
        if (cardPlay?.IsAutoPlay != false) return Task.CompletedTask;
        Player player = cardPlay?.Card?.Owner;
        if (Amount <= 0 || Owner?.IsAlive != true || player?.Creature.IsAlive != true ||
            Owner.Monster is not NararaShibaBossMonster || player.Creature.Side == Owner.Side ||
            !ReferenceEquals(player.Creature.CombatState, Owner.CombatState) ||
            player.PlayerCombatState == null || Owner.GetPower<NararaSleepingPower>()?.Amount > 0)
            return Task.CompletedTask;

        if (RecordCardPlayed(player)) _pendingCopies.Add(cardPlay);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Player player = cardPlay?.Card?.Owner;
        if (cardPlay == null || !_pendingCopies.Remove(cardPlay) || Amount <= 0 ||
            Owner?.IsAlive != true || player?.Creature.IsAlive != true ||
            player.PlayerCombatState == null ||
            !ReferenceEquals(player.Creature.CombatState, Owner.CombatState)) return;

        // Eligibility is the Unplayable keyword, not whether this player can
        // currently pay for the card or meet its temporary play conditions.
        // Fragile temporarily removes Unplayable during automatic play, but
        // remains ineligible while a teammate's pending copy resolves.
        CardModel[] candidates = player.PlayerCombatState.Hand.Cards
            .Where(card => card is not FragileHbrgoSaki &&
                !card.Keywords.Contains(CardKeyword.Unplayable))
            .ToArray();
        if (candidates.Length == 0) return;

        CardModel source = player.RunState.Rng.CombatCardSelection.NextItem(candidates);
        CardModel copy = CreateGiftCopy(source);
        Flash();
        await CardPileCmd.AddGeneratedCardToCombat(
            copy, PileType.Draw, null, CardPilePosition.Random);
    }

    internal static CardModel CreateGiftCopy(CardModel source)
    {
        // Native combat clones retain upgrades, enchantments and local costs,
        // without modifying the source card or creating a permanent deck copy.
        CardModel copy = source.CreateClone();
        NararaGiftCopyHelper.MarkCopy(source, copy);
        copy.InvokeEnergyCostChanged();
        return copy;
    }

    internal bool RecordCardPlayed(Player player)
    {
        int count = _cardsPlayedByPlayer.GetValueOrDefault(player) + 1;
        _cardsPlayedByPlayer[player] = count % CardsPerCopy;
        RefreshCountdown();
        return count == CardsPerCopy;
    }

    internal int GetCardsUntilTrigger(Player player) =>
        player == null || !IsMutable || Owner?.GetPower<NararaSleepingPower>()?.Amount > 0
            ? CardsPerCopy
            : CardsPerCopy - _cardsPlayedByPlayer.GetValueOrDefault(player);

    internal void RefreshCountdown() => InvokeDisplayAmountChanged();

    internal void ResetAwakeCounters()
    {
        _cardsPlayedByPlayer.Clear();
        RefreshCountdown();
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _cardsPlayedByPlayer = new();
        _pendingCopies = new();
    }
}
