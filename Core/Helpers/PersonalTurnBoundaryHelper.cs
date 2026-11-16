using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PersonalTurnBoundaryHelper
{
    private static Creature[] Participants(Player owner) => owner.Creature.CombatState.Allies
        .Where(creature => creature.IsAlive && (ReferenceEquals(creature, owner.Creature) ||
            ReferenceEquals(creature.PetOwner, owner))).ToArray();

    private static PowerModel[] Debuffs(Creature[] participants) => participants.SelectMany(creature => creature.Powers)
        .Where(power => power.TypeForCurrentAmount == PowerType.Debuff).ToArray();

    internal static async Task Start(PlayerChoiceContext context, Player owner)
    {
        Creature[] participants = Participants(owner);
        var combat = owner.Creature.CombatState;
        // OD does not replay native side-wide start/end. Explicitly dispatch
        // only this participant's negative powers, so they keep their actual
        // Poison/Calamity/duration rules without firing teammate turn hooks.
        foreach (PowerModel power in Debuffs(participants))
            if (CanRun(owner, power, participants))
                await power.BeforeSideTurnStart(context, CombatSide.Player, participants, combat);
        foreach (PowerModel power in Debuffs(participants))
            if (CanRun(owner, power, participants))
                await power.AfterSideTurnStart(CombatSide.Player, participants, combat);
        foreach (PowerModel power in Debuffs(participants))
            if (CanRun(owner, power, participants))
                await power.AfterPlayerTurnStart(context, owner);
    }

    internal static async Task Flush(PlayerChoiceContext context, Player owner, object token)
    {
        await SakuraRainPower.TriggerInsertedTurnEnd(context, owner, token);
        if (!CanRun(owner)) return;
        if (owner.GetRelic<Millsage>() is { } millsage) await millsage.TriggerOverclockTurnEnd(context, token);
        if (!CanRun(owner)) return;
        CardPile hand = owner.PlayerCombatState.Hand;
        KanzakiShowtimeCombatHelper.OnTurnEnd(owner, token);
        foreach (CardModel card in hand.Cards.ToArray())
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding) return;
            if (card.Pile != hand) continue;
            if (card.HasTurnEndInHandEffect) await card.OnTurnEndInHandWrapper(context);
            else if (card.Keywords.Contains(CardKeyword.Ethereal) && Hook.ShouldEtherealTrigger(owner.Creature.CombatState, card))
                await OverclockCardCommandHelper.ExhaustEthereal(context, card);
        }
        if (!CanRun(owner)) return;
        KurosawaLeaderCombatHelper.BeforeFlush(owner);
        CardModel[] flushing = hand.Cards.Where(card => !KurosawaLeaderCombatHelper.KeepsForExtraFlush(owner, card)).ToArray();
        if (flushing.Length > 0) await CardPileCmd.Add(flushing, PileType.Discard);
        if (!CanRun(owner)) return;
        await KurosawaLeaderCombatHelper.AfterFlush(context, owner);
        if (!CanRun(owner)) return;
        await HotaruSeeYouAgainHelper.OnTurnEnd(context, owner, token);
        if (!CanRun(owner)) return;
        await HotaruEchoLocationPower.TurnEnd(context, owner, token);
        if (!CanRun(owner)) return;
        await HotaruCommonCannonHelper.OnTurnEnd(context, owner, token);
        if (!CanRun(owner)) return;
        await HotaruUncommonCardHelper.OnTurnEnd(context, owner, token);
        if (!CanRun(owner)) return;
        await HotaruUncommonCannonHelper.OnTurnEnd(context, owner, token);
        if (!CanRun(owner)) return;
        await HotaruRareRollHelper.OnTurnEnd(context, owner, token);
        if (!CanRun(owner)) return;
        await HotaruRareCannonHelper.OnTurnEnd(context, owner, token);
        if (!CanRun(owner)) return;
        Creature[] participants = Participants(owner);
        foreach (PowerModel power in Debuffs(participants))
            if (CanRun(owner, power, participants)) await power.BeforeSideTurnEnd(context, CombatSide.Player, participants);
        foreach (PowerModel power in Debuffs(participants))
            if (CanRun(owner, power, participants)) await power.AfterSideTurnEnd(context, CombatSide.Player, participants);
        foreach (PowerModel power in Debuffs(participants))
            if (CanRun(owner, power, participants)) await power.AfterSideTurnEndLate(context, CombatSide.Player, participants);
    }

    private static bool CanRun(Player owner) => owner?.Creature is { IsAlive: true, CombatState: { } combat } &&
        CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsOverOrEnding &&
        ReferenceEquals(CombatManager.Instance.DebugOnlyGetState(), combat);

    private static bool CanRun(Player owner, PowerModel power, Creature[] participants) => CanRun(owner) &&
        power?.Owner is { IsAlive: true } holder && participants.Contains(holder) && holder.Powers.Contains(power);
}
