using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ModeTransitionHelper
{
    private static int _suppressDualSwordsOnModeExitDepth;

    public static IDisposable SuppressDualSwordsOnModeExit()
    {
        _suppressDualSwordsOnModeExitDepth++;
        return new SuppressionScope();
    }

    public static async Task ReplaceChihayaInLibraryWithSwitch(Creature owner, CardModel excludedCard = null)
    {
        Player player = owner?.Player;
        if (player?.PlayerCombatState == null || owner.CombatState == null)
        {
            return;
        }

        List<ChihayaClassmateHbrgoSaki> chihayaCards = GetChihayaCardsToReplace(player, excludedCard).ToList();
        foreach (ChihayaClassmateHbrgoSaki chihayaCard in chihayaCards)
        {
            PileType pileType = GetSwitchPileType(chihayaCard);
            SwitchHbrgoSaki switchCard = owner.CombatState.CreateCard<SwitchHbrgoSaki>(player);
            SyncUpgradeLevel(switchCard, chihayaCard);

            await CardPileCmd.AddGeneratedCardToCombat(switchCard, pileType, player);
            if (!chihayaCard.HasBeenRemovedFromState && chihayaCard.Pile?.IsCombatPile == true)
            {
                await CardPileCmd.RemoveFromCombat(chihayaCard, skipVisuals: true);
            }
        }
    }

    public static bool ShouldKeepSwitchOutOfHand(Creature owner)
    {
        return owner?.HasPower<MagicSwordModePower>() == true ||
               owner?.HasPower<CrimsonBladeFlowPower>() == true;
    }

    public static async Task MoveSwitchesOutOfHandWhileBlocked(Creature owner, PileType destinationPileType = PileType.Discard)
    {
        Player player = owner?.Player;
        if (!ShouldKeepSwitchOutOfHand(owner) || player?.PlayerCombatState == null || owner.CombatState == null)
        {
            return;
        }

        List<SwitchHbrgoSaki> handSwitches = player.PlayerCombatState.AllCards
            .OfType<SwitchHbrgoSaki>()
            .Where(card => card.Pile?.Type == PileType.Hand)
            .ToList();

        foreach (SwitchHbrgoSaki handSwitch in handSwitches)
        {
            SwitchHbrgoSaki switchCard = owner.CombatState.CreateCard<SwitchHbrgoSaki>(player);
            SyncUpgradeLevel(switchCard, handSwitch);

            await CardPileCmd.AddGeneratedCardToCombat(switchCard, destinationPileType, player);
            if (!handSwitch.HasBeenRemovedFromState && handSwitch.Pile?.IsCombatPile == true)
            {
                await CardPileCmd.RemoveFromCombat(handSwitch, skipVisuals: true);
            }
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

    private static IEnumerable<ChihayaClassmateHbrgoSaki> GetChihayaCardsToReplace(Player player, CardModel excludedCard)
    {
        IEnumerable<ChihayaClassmateHbrgoSaki> drawPileCards = player.PlayerCombatState.DrawPile.Cards
            .OfType<ChihayaClassmateHbrgoSaki>();

        IEnumerable<ChihayaClassmateHbrgoSaki> otherLibraryCards = player.PlayerCombatState.AllCards
            .OfType<ChihayaClassmateHbrgoSaki>()
            .Where(card => card.Pile?.IsCombatPile == true &&
                           card.Pile.Type != PileType.Play);

        return drawPileCards
            .Concat(otherLibraryCards)
            .Where(card => card != excludedCard &&
                           !card.HasBeenRemovedFromState &&
                           card.Pile?.IsCombatPile == true)
            .Distinct();
    }

    private static PileType GetSwitchPileType(ChihayaClassmateHbrgoSaki chihayaCard)
    {
        return chihayaCard.Pile?.Type == PileType.Hand ? PileType.Discard : chihayaCard.Pile.Type;
    }

    private static void SyncUpgradeLevel(CardModel target, CardModel source)
    {
        while (target.CurrentUpgradeLevel < source.CurrentUpgradeLevel && target.IsUpgradable)
        {
            target.UpgradeInternal();
            target.FinalizeUpgradeInternal();
        }
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
}
