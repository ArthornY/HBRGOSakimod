using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CrushYouLikeABugCombatHelper
{
    internal sealed class PlayScope
    {
        internal PlayScope Previous;
        internal CardModel Card;
        internal int PaidEnergy;
        internal bool Refunded;
        internal bool Active = true;
    }
    private sealed class PlayFlags { internal bool Refunded; }
    private static readonly AsyncLocal<PlayScope> Current = new();
    private static readonly ConditionalWeakTable<CardPlay, PlayFlags> DirectPlays = new();

    internal static decimal GetDpDamageMultiplier(CardModel cardSource, Creature target, ValueProp props)
    {
        if (cardSource is not CrushYouLikeABugHbrgoSaki || target == null || !props.IsPoweredAttack())
            return 1m;
        // Evaluated by the actual damage calculation for every target/segment.
        // Keep exact DP, rather than the native display proxy or its maximum.
        return target.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m ||
            target.Player?.GetRelic<DeflectorProtection>()?.Dp > 0 ? 1.5m : 1m;
    }

    internal static decimal GetDestructionRateMultiplier(CardModel cardSource) =>
        cardSource is CrushYouLikeABugHbrgoSaki card && card.HasUpgradeEffect(3) ? 2m : 1m;

    internal static PlayScope BeginPlay(CardModel card, ResourceInfo resources)
    {
        if (card is not CrushYouLikeABugHbrgoSaki) return null;
        PlayScope scope = new() { Previous = Current.Value, Card = card,
            PaidEnergy = Math.Max(0, resources.EnergySpent) };
        Current.Value = scope;
        return scope;
    }
    internal static void RestoreCaller(PlayScope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
    }
    internal static async Task FinishPlay(Task original, PlayScope scope)
    {
        try { await original; }
        finally { ClosePlay(scope); }
    }
    internal static void ClosePlay(PlayScope scope)
    {
        if (scope != null) scope.Active = false;
        RestoreCaller(scope);
    }

    private static int TakeRefund(CrushYouLikeABugHbrgoSaki card, CardPlay play)
    {
        if (play?.IsFirstInSeries != true || !ReferenceEquals(play.Card, card)) return 0;
        for (PlayScope scope = Current.Value; scope != null; scope = scope.Previous)
        {
            if (!scope.Active || !ReferenceEquals(scope.Card, card)) continue;
            if (scope.Refunded) return 0;
            scope.Refunded = true;
            return Math.Min(scope.PaidEnergy, Math.Max(0, play.Resources.EnergySpent));
        }
        // Direct core calls still have one actual CardPlay payment record.
        PlayFlags flags = DirectPlays.GetOrCreateValue(play);
        if (flags.Refunded) return 0;
        flags.Refunded = true;
        return Math.Max(0, play.Resources.EnergySpent);
    }

    internal static async Task FinishUpgradeEffects(PlayerChoiceContext context,
        CrushYouLikeABugHbrgoSaki card, CardPlay play)
    {
        if (card?.Owner?.Creature?.IsAlive != true) return;
        if (card.HasUpgradeEffect(5))
        {
            int refund = TakeRefund(card, play);
            if (refund > 0) await PlayerCmd.GainEnergy(refund, card.Owner);
        }
        if (card.HasUpgradeEffect(6) && card.Owner.Creature.IsAlive)
            await PowerCmd.Apply<CrushYouLikeABugReplayPower>(context,
                card.Owner.Creature, 1m, card.Owner.Creature, card);
    }
}
