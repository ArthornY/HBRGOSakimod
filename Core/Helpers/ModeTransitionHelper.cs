using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal enum ReptureCardMode
{
    None,
    Bow,
    DualSwords,
    Scythe
}

internal readonly record struct ModeTransitionPlayState(
    ReptureCardMode RequiredMode,
    QuickSwitchPower QuickSwitch,
    SwiftPower Swift,
    bool HasEffectContext);

internal readonly record struct ModeTransitionDamagePreviewState(
    CardModel Card,
    decimal BaseDamageMultiplier,
    decimal ModeDamageMultiplier);

internal static class ModeTransitionHelper
{
    private static int _suppressDualSwordsOnModeExitDepth;
    private static readonly Dictionary<CardModel, Stack<bool>>
        _effectHalvingContexts = new(
            ReferenceEqualityComparer.Instance);

    [ThreadStatic]
    private static Stack<ModeTransitionDamagePreviewState>
        _damagePreviewContexts;

    public static IDisposable SuppressDualSwordsOnModeExit()
    {
        _suppressDualSwordsOnModeExitDepth++;
        return new SuppressionScope();
    }

    public static bool CanPlayBowRestrictedCard(Creature owner)
    {
        return owner != null;
    }

    public static bool CanPlayDualSwordsRestrictedCard(Creature owner)
    {
        return owner != null;
    }

    public static bool CanPlayScytheRestrictedCard(Creature owner)
    {
        return owner != null;
    }

    public static bool IsBowRestrictedCard(CardModel card)
    {
        return card is ArrowRainHbrgoSaki
            or SlideStepHbrgoSaki
            or StrongShotHbrgoSaki
            or MimicExorcismArrowHbrgoSaki
            or MimicIceFlowerThousandArrowsHbrgoSaki
            or MimicFullBodyShotHbrgoSaki
            or FragileDoubleStrikeHbrgoSaki
            or HangingShotHbrgoSaki
            or PiercingShotHbrgoSaki
            or ChargedShotHbrgoSaki;
    }

    public static bool IsDualSwordsRestrictedCard(CardModel card)
    {
        return card is CrossSlashHbrgoSaki
            or SwallowReversalHbrgoSaki
            or BladeDanceHbrgoSaki
            or MimicDreamBubbleHbrgoSaki
            or NextTurnEnergyStrikeHbrgoSaki
            or RollBoostStrikeHbrgoSaki
            or MimicThunderGaleHbrgoSaki
            or MimicSaviorStrikeHbrgoSaki
            or SelfSacrificeSlashHbrgoSaki;
    }

    public static bool IsScytheRestrictedCard(CardModel card)
    {
        return card is SoulHarvestHbrgoSaki
            or SoulExplosionHbrgoSaki
            or MimicCursedScytheHbrgoSaki
            or ExtraGaugeStrikeHbrgoSaki
            or ArmorBreakSoulStrikeHbrgoSaki
            or SoulFinisherHbrgoSaki
            or DpSoulStrikeHbrgoSaki;
    }

    public static bool IsModeRestrictedCard(CardModel card)
    {
        return IsBowRestrictedCard(card) ||
            IsDualSwordsRestrictedCard(card) ||
            IsScytheRestrictedCard(card);
    }

    public static bool IsCardCompatibleWithCurrentMode(
        Creature owner,
        CardModel card)
    {
        ReptureCardMode requiredMode = GetRequiredMode(card);
        return requiredMode == ReptureCardMode.None ||
            IsModeCompatible(owner, requiredMode);
    }

    public static ReptureCardMode GetOffModeTransition(CardModel card)
    {
        ReptureCardMode requiredMode = GetRequiredMode(card);
        Creature owner = card?.Owner?.Creature;
        return requiredMode != ReptureCardMode.None &&
               !IsModeCompatible(owner, requiredMode)
            ? requiredMode
            : ReptureCardMode.None;
    }

    public static decimal GetAdjustedEffectAmount(
        CardModel card,
        decimal amount)
    {
        if (TryGetCurrentEffectHalving(card, out bool halve))
        {
            return halve ? amount * 0.5m : amount;
        }

        return GetOffModeTransition(card) == ReptureCardMode.None
            ? amount
            : amount * 0.5m;
    }

    public static IDisposable BeginDamagePreview(
        CardModel card,
        bool runGlobalHooks)
    {
        if (!runGlobalHooks)
        {
            return null;
        }

        ReptureCardMode requiredMode =
            GetOffModeTransition(card);
        Creature owner = card?.Owner?.Creature;
        if (requiredMode == ReptureCardMode.None ||
            owner == null)
        {
            return null;
        }

        bool preventsHalving =
            owner.HasPower<NormalStatePower>() ||
            owner.GetPower<QuickSwitchPower>()?.Amount > 0 ||
            owner.GetPower<SwiftPower>()?.CanPreventNextPenalty == true;
        decimal modeDamageMultiplier = requiredMode switch
        {
            ReptureCardMode.Bow =>
                ReptureBowPower.GetProjectedDamageMultiplier(
                    owner,
                    card),
            ReptureCardMode.DualSwords =>
                ReptureDualSwordsPower
                    .GetProjectedDamageMultiplier(owner),
            _ => 1m
        };

        _damagePreviewContexts ??=
            new Stack<ModeTransitionDamagePreviewState>();
        _damagePreviewContexts.Push(
            new ModeTransitionDamagePreviewState(
                card,
                preventsHalving ? 1m : 0.5m,
                modeDamageMultiplier));
        return new DamagePreviewScope();
    }

    public static bool TryGetDamagePreview(
        CardModel card,
        out ModeTransitionDamagePreviewState state)
    {
        state = default;
        return card != null &&
               _damagePreviewContexts?.TryPeek(out state) == true &&
               ReferenceEquals(state.Card, card);
    }

    public static ModeTransitionPlayState BeginCardPlay(
        CardModel card)
    {
        ReptureCardMode requiredMode =
            GetOffModeTransition(card);
        Creature owner = card?.Owner?.Creature;
        bool preventHalving =
            requiredMode != ReptureCardMode.None &&
            owner?.HasPower<NormalStatePower>() == true;
        SwiftPower swift = requiredMode != ReptureCardMode.None
            ? owner?.GetPower<SwiftPower>()
            : null;
        QuickSwitchPower quickSwitch = null;
        if (!preventHalving &&
            swift?.TryReserve(card) == true)
        {
            preventHalving = true;
        }

        if (requiredMode != ReptureCardMode.None &&
            !preventHalving)
        {
            QuickSwitchPower candidate =
                owner?.GetPower<QuickSwitchPower>();
            if (candidate?.TryReserve(card) == true)
            {
                quickSwitch = candidate;
                preventHalving = true;
            }
        }

        PushEffectHalvingContext(
            card,
            requiredMode != ReptureCardMode.None &&
            !preventHalving);
        return new ModeTransitionPlayState(
            requiredMode,
            quickSwitch,
            swift,
            card != null);
    }

    public static void EndCardPlay(
        CardModel card,
        ModeTransitionPlayState state)
    {
        if (!state.HasEffectContext ||
            card == null ||
            !_effectHalvingContexts.TryGetValue(
                card,
                out Stack<bool> contexts))
        {
            return;
        }

        contexts.Pop();
        if (contexts.Count == 0)
        {
            _effectHalvingContexts.Remove(card);
        }
    }

    public static async Task<bool> EnterRequiredModeBeforePlay(
        PlayerChoiceContext choiceContext,
        CardModel card,
        ReptureCardMode requiredMode)
    {
        Creature owner = card?.Owner?.Creature;
        if (requiredMode == ReptureCardMode.None ||
            owner?.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding ||
            IsModeCompatible(owner, requiredMode))
        {
            return false;
        }

        switch (requiredMode)
        {
            case ReptureCardMode.Bow:
                await PowerCmd.Apply<ReptureBowPower>(
                    choiceContext,
                    owner,
                    1m,
                    owner,
                    card);
                break;
            case ReptureCardMode.DualSwords:
                await PowerCmd.Apply<ReptureDualSwordsPower>(
                    choiceContext,
                    owner,
                    1m,
                    owner,
                    card);
                break;
            case ReptureCardMode.Scythe:
                await PowerCmd.Apply<RaptureScythePower>(
                    choiceContext,
                    owner,
                    1m,
                    owner,
                    card);
                break;
        }

        return true;
    }

    private static void PushEffectHalvingContext(
        CardModel card,
        bool halve)
    {
        if (card == null)
        {
            return;
        }

        if (!_effectHalvingContexts.TryGetValue(
                card,
                out Stack<bool> contexts))
        {
            contexts = new Stack<bool>();
            _effectHalvingContexts.Add(card, contexts);
        }

        contexts.Push(halve);
    }

    private static bool TryGetCurrentEffectHalving(
        CardModel card,
        out bool halve)
    {
        halve = false;
        return card != null &&
               _effectHalvingContexts.TryGetValue(
                   card,
                   out Stack<bool> contexts) &&
               contexts.TryPeek(out halve);
    }

    public static bool IsCardIncompatibleWithCurrentMode(Creature owner, CardModel card)
    {
        if (owner == null || card?.Owner?.Creature != owner)
        {
            return false;
        }

        if (owner.HasPower<MagicSwordModePower>() ||
            owner.HasPower<CrimsonBladeFlowPower>())
        {
            return false;
        }

        return false;
    }

    public static async Task<int> DiscardIncompatibleCardsFromHandAndRedraw(Creature owner)
    {
        Player player = owner?.Player;
        if (player?.PlayerCombatState == null)
        {
            return 0;
        }

        List<CardModel> incompatibleCards = player.PlayerCombatState.Hand.Cards
            .Where(card => IsCardIncompatibleWithCurrentMode(owner, card))
            .ToList();
        if (incompatibleCards.Count == 0)
        {
            return 0;
        }

        await CardPileCmd.Add(incompatibleCards, PileType.Discard);
        await CardPileCmd.Draw(
            new ThrowingPlayerChoiceContext(),
            incompatibleCards.Count,
            player);
        return incompatibleCards.Count;
    }

    public static async Task HandleIncompatibleCardEnteredHand(
        Creature owner,
        CardModel card,
        PileType oldPileType)
    {
        Player player = owner?.Player;
        if (player?.PlayerCombatState == null ||
            card?.Pile?.Type != PileType.Hand ||
            !IsCardIncompatibleWithCurrentMode(owner, card))
        {
            return;
        }

        await CardPileCmd.Add(card, PileType.Discard);
        if (oldPileType != PileType.Draw ||
            CombatManager.Instance.IsOverOrEnding ||
            !HasCompatibleCardAvailableToDraw(owner, card))
        {
            return;
        }

        await CardPileCmd.Draw(
            new ThrowingPlayerChoiceContext(),
            1m,
            player);
    }

    public static async Task RemoveModeSelectionCardsFromCombat(Creature owner)
    {
        Player player = owner?.Player;
        if (player?.PlayerCombatState == null)
        {
            return;
        }

        List<CardModel> modeCards = player.PlayerCombatState.AllCards
            .Where(card => card is ChihayaClassmateHbrgoSaki or SwitchHbrgoSaki)
            .Where(card => card.Pile?.IsCombatPile == true && card.Pile.Type != PileType.Play)
            .ToList();
        if (modeCards.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(modeCards);
        }
    }

    public static async Task EnterDualSwordsAfterModeExit(Creature owner)
    {
        if (_suppressDualSwordsOnModeExitDepth > 0 ||
            owner?.CombatState == null ||
            owner.Player?.PlayerCombatState == null ||
            owner.HasPower<ReptureBowPower>() ||
            owner.HasPower<ReptureDualSwordsPower>())
        {
            return;
        }

        await PowerCmd.Apply<ReptureDualSwordsPower>(
            new ThrowingPlayerChoiceContext(),
            owner,
            1m,
            owner,
            null);
    }

    private static ReptureCardMode GetRequiredMode(CardModel card)
    {
        if (IsBowRestrictedCard(card))
        {
            return ReptureCardMode.Bow;
        }

        if (IsDualSwordsRestrictedCard(card))
        {
            return ReptureCardMode.DualSwords;
        }

        return IsScytheRestrictedCard(card)
            ? ReptureCardMode.Scythe
            : ReptureCardMode.None;
    }

    private static bool IsModeCompatible(
        Creature owner,
        ReptureCardMode requiredMode)
    {
        if (owner == null)
        {
            return false;
        }

        if (owner.HasPower<MagicSwordModePower>() ||
            owner.HasPower<CrimsonBladeFlowPower>())
        {
            return true;
        }

        return requiredMode switch
        {
            ReptureCardMode.Bow =>
                owner.HasPower<ReptureBowPower>(),
            ReptureCardMode.DualSwords =>
                owner.HasPower<ReptureDualSwordsPower>(),
            ReptureCardMode.Scythe =>
                owner.HasPower<RaptureScythePower>(),
            _ => true
        };
    }

    private static bool HasCompatibleCardAvailableToDraw(Creature owner, CardModel excludedCard)
    {
        PlayerCombatState combatState = owner?.Player?.PlayerCombatState;
        if (combatState == null)
        {
            return false;
        }

        return combatState.DrawPile.Cards
            .Concat(combatState.DiscardPile.Cards)
            .Any(card =>
                card != excludedCard &&
                !card.HasBeenRemovedFromState &&
                !IsCardIncompatibleWithCurrentMode(owner, card) &&
                card is not ChihayaClassmateHbrgoSaki &&
                card is not SwitchHbrgoSaki);
    }

    private sealed class SuppressionScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _suppressDualSwordsOnModeExitDepth = Math.Max(0, _suppressDualSwordsOnModeExitDepth - 1);
        }
    }

    private sealed class DamagePreviewScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_damagePreviewContexts?.Count > 0)
            {
                _damagePreviewContexts.Pop();
            }
        }
    }
}
