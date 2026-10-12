using System.Collections.Generic;
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
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NararaOverworkPower : PowerModel, ICustomPower
{
    internal const int CardsPerOverwork = 7;

    private Dictionary<Player, int> _cardsPlayedByPlayer = new();
    private HashSet<CardPlay> _pendingOverwork = new();
    private NararaSleepingPower _countedSleep;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount
    {
        get
        {
            if (!IsMutable || Owner?.CombatState is not { } combat) return CardsPerOverwork;
            Player player = LocalContext.GetMe(combat.Players) ??
                (combat.Players.Count == 1 ? combat.Players[0] : null);
            return GetCardsUntilTrigger(player);
        }
    }

    public string CustomPackedIconPath => OverworkPortraitHelper.PortraitPath;
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        HoverTipFactory.FromCardWithCardHoverTips<OverworkHbrgoSaki>();

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // Triggered cards (including every automatic Fragile play from a
        // multi-hit attack) must not advance the manual-card countdown.
        if (cardPlay?.IsAutoPlay != false) return Task.CompletedTask;
        Player player = cardPlay?.Card?.Owner;
        if (Amount <= 0 || Owner?.IsAlive != true || player?.Creature.IsAlive != true ||
            Owner.Monster is not NararaShibaBossMonster || player.Creature.Side == Owner.Side ||
            !ReferenceEquals(player.Creature.CombatState, Owner.CombatState)) return Task.CompletedTask;

        NararaSleepingPower sleep = Owner.GetPower<NararaSleepingPower>();
        if (sleep == null || sleep.Amount <= 0) return Task.CompletedTask;

        // The ability remains visible while awake. A new sleep starts a fresh
        // counter for every player, while ordinary/extra turns preserve it.
        if (!ReferenceEquals(_countedSleep, sleep))
        {
            _countedSleep = sleep;
            _cardsPlayedByPlayer.Clear();
        }
        if (RecordCardPlayed(player)) _pendingOverwork.Add(cardPlay);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Player player = cardPlay?.Card?.Owner;
        if (cardPlay == null || !_pendingOverwork.Remove(cardPlay) || Amount <= 0 ||
            Owner?.IsAlive != true || player?.Creature.IsAlive != true ||
            player.PlayerCombatState == null ||
            !ReferenceEquals(player.Creature.CombatState, Owner.CombatState)) return;

        // Automatic plays never reserve a reward in BeforeCardPlayed.
        // The newly generated card never penalizes the play that created it.
        Flash();
        CardModel overwork = Owner.CombatState.CreateCard<OverworkHbrgoSaki>(player);
        await CardPileCmd.AddGeneratedCardToCombat(overwork, PileType.Hand, null);
    }

    internal bool RecordCardPlayed(Player player)
    {
        int count = _cardsPlayedByPlayer.GetValueOrDefault(player) + 1;
        _cardsPlayedByPlayer[player] = count % CardsPerOverwork;
        RefreshCountdown();
        return count == CardsPerOverwork;
    }

    internal int GetCardsUntilTrigger(Player player)
    {
        // The next sleep has a fresh counter even before its first card is played.
        // Reading the badge must not mutate the current phase's counters.
        if (player == null || !IsMutable ||
            Owner?.GetPower<NararaSleepingPower>() is not { Amount: > 0 } sleep ||
            !ReferenceEquals(_countedSleep, sleep)) return CardsPerOverwork;
        return CardsPerOverwork - _cardsPlayedByPlayer.GetValueOrDefault(player);
    }

    internal void RefreshCountdown() => InvokeDisplayAmountChanged();

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _cardsPlayedByPlayer = new();
        _pendingOverwork = new();
        _countedSleep = null;
    }
}
