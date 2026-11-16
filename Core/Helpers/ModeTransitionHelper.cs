using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
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
    ReptureCardMode RequiredMode);

internal readonly record struct ModeTransitionDamagePreviewState(
    CardModel Card,
    decimal ModeDamageMultiplier);

internal static class ModeTransitionHelper
{
    private static int _suppressDualSwordsOnModeExitDepth;
    private static readonly Dictionary<CardModel, int>
        _modeTransitionSuppressionDepths = new(
            ReferenceEqualityComparer.Instance);

    [ThreadStatic]
    private static Stack<ModeTransitionDamagePreviewState>
        _damagePreviewContexts;

    internal static async Task RecordModeSwitch(Creature owner)
    {
        if (owner == null) return;
        if (owner.GetPower<SwiftPower>() is { } swift) await swift.RecordModeSwitch();
        if (owner.Player?.GetRelic<MetronomeThirtyOneO>() is { } metronome) await metronome.RecordModeSwitch();
    }

    public static IDisposable SuppressDualSwordsOnModeExit()
    {
        _suppressDualSwordsOnModeExitDepth++;
        return new SuppressionScope();
    }

    public static IDisposable SuppressModeTransition(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _modeTransitionSuppressionDepths.TryGetValue(
            card,
            out int depth);
        _modeTransitionSuppressionDepths[card] = depth + 1;
        return new ModeTransitionSuppressionScope(card);
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

    // The bow and dual swords keep working while the magic sword is open (design v3);
    // only 绯刀流 still weakens them.
    public static bool IsWeaponModeWeakened(Creature owner)
    {
        return CrimsonBladeFlowPower.IsActive(owner);
    }

    public static bool IsBowRestrictedCard(CardModel card)
    {
        return card is SlideStepHbrgoSaki
            or StrongShotHbrgoSaki
            or DragonArrowHbrgoSaki
            or MimicExorcismArrowHbrgoSaki
            or MimicIceFlowerThousandArrowsHbrgoSaki
            or MimicFullBodyShotHbrgoSaki
            or FragileDoubleStrikeHbrgoSaki
            or HangingShotHbrgoSaki
            or PiercingShotHbrgoSaki
            or ChargedShotHbrgoSaki
            or DestinedArrowHbrgoSaki
            or MimicDivineCraftHbrgoSaki;
    }

    public static bool IsDualSwordsRestrictedCard(CardModel card)
    {
        return card is SwallowReversalHbrgoSaki
            or SwitchingSlashHbrgoSaki
            or ConsecutiveSlashHbrgoSaki
            or BladeDanceHbrgoSaki
            or MimicDreamBubbleHbrgoSaki
            or NextTurnEnergyStrikeHbrgoSaki
            or RollBoostStrikeHbrgoSaki
            or MimicThunderGaleHbrgoSaki
            or MimicSaviorStrikeHbrgoSaki
            or SelfSacrificeSlashHbrgoSaki
            or MimicWillActivationHbrgoSaki;
    }

    public static bool IsScytheRestrictedCard(CardModel card)
    {
        return card is SoulHarvestHbrgoSaki
            or SoulExplosionHbrgoSaki
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
        ReptureCardMode requiredMode = GetCardMode(card);
        return requiredMode == ReptureCardMode.None ||
            IsRequiredModeSatisfied(owner, card, requiredMode);
    }

    public static ReptureCardMode GetOffModeTransition(CardModel card)
    {
        ReptureCardMode requiredMode = GetCardMode(card);
        Creature owner = card?.Owner?.Creature;
        return requiredMode != ReptureCardMode.None &&
               !IsRequiredModeSatisfied(owner, card, requiredMode)
            ? requiredMode
            : ReptureCardMode.None;
    }

    public static decimal GetAdjustedEffectAmount(
        CardModel card,
        decimal amount)
    {
        // Mode cards always resolve their authored amount. Weapon modes
        // still apply their own outgoing damage/block modifiers afterward.
        return amount;
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
        return new ModeTransitionPlayState(
            IsModeTransitionSuppressed(card)
                ? ReptureCardMode.None
                : GetOffModeTransition(card));
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
            IsRequiredModeSatisfied(owner, card, requiredMode))
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

    private static bool IsModeTransitionSuppressed(CardModel card)
    {
        return card != null &&
               _modeTransitionSuppressionDepths.TryGetValue(
                   card,
                   out int depth) &&
               depth > 0;
    }

    private static void EndModeTransitionSuppression(CardModel card)
    {
        if (!_modeTransitionSuppressionDepths.TryGetValue(
                card,
                out int depth))
        {
            return;
        }

        if (depth <= 1)
        {
            _modeTransitionSuppressionDepths.Remove(card);
            return;
        }

        _modeTransitionSuppressionDepths[card] = depth - 1;
    }

    public static bool IsCardIncompatibleWithCurrentMode(Creature owner, CardModel card)
    {
        if (owner == null || card?.Owner?.Creature != owner)
        {
            return false;
        }

        if (owner.HasPower<MagicSwordModePower>() ||
            CrimsonBladeFlowPower.IsActive(owner))
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
            .Where(card => card is ChihayaClassmateHbrgoSaki)
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
            IsWeaponModeWeakened(owner) ||
            owner.HasPower<ReptureBowPower>() ||
            owner.HasPower<ReptureDualSwordsPower>() ||
            owner.HasPower<RaptureScythePower>())
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

    public static async Task<CardModel> DrawModeRestrictedCard(
        PlayerChoiceContext choiceContext,
        Player player,
        ReptureCardMode mode,
        AbstractModel source)
    {
        CardPile drawPile = player?.PlayerCombatState?.DrawPile;
        CardModel restrictedCard = drawPile?.Cards.FirstOrDefault(
            card => MagicSwordCombatTemplateHelper.CanGenerateFromDeck(card) &&
                GetCardMode(card) == mode);
        if (restrictedCard == null)
        {
            return null;
        }

        await CardPileCmd.Add(
            restrictedCard,
            drawPile,
            CardPilePosition.Top,
            source,
            skipVisuals: true);
        return await CardPileCmd.Draw(choiceContext, player);
    }

    internal static ReptureCardMode GetCardMode(CardModel card)
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

    private static bool IsRequiredModeSatisfied(
        Creature owner,
        CardModel card,
        ReptureCardMode requiredMode)
    {
        if (card is not SwitchingSlashHbrgoSaki)
        {
            return IsModeCompatible(owner, requiredMode);
        }

        return owner != null &&
               (requiredMode switch
               {
                   ReptureCardMode.Bow =>
                       owner.HasPower<ReptureBowPower>(),
                   ReptureCardMode.DualSwords =>
                       owner.HasPower<ReptureDualSwordsPower>(),
                   ReptureCardMode.Scythe =>
                       owner.HasPower<RaptureScythePower>(),
                   _ => true
               });
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
                card is not ChihayaClassmateHbrgoSaki);
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

    private sealed class ModeTransitionSuppressionScope : IDisposable
    {
        private readonly CardModel _card;
        private bool _disposed;

        public ModeTransitionSuppressionScope(CardModel card)
        {
            _card = card;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            EndModeTransitionSuppression(_card);
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
