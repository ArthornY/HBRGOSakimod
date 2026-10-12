using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Screens;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Helpers;

internal sealed record MemoryGardenChoice(
    CardModel PreviewCard,
    string CharacterName,
    bool IsUpgrade,
    bool IsAvailable,
    string StatusText);

internal static partial class HbrMemoryGarden
{
    private sealed record CharacterEntry(string Id, string Name, Type CardType);

    private static readonly CharacterEntry[] Characters =
    {
        new("taki", "椎名立希", typeof(HerSongIsMyScreamHbrgoSaki)),
        new("rana", "要乐奈", typeof(MatchaParfaitHbrgoSaki)),
        new("tomori", "高松灯", typeof(LostStarSongHbrgoSaki)),
        new("anon", "千早爱音", typeof(AnonAidHbrgoSaki)),
        new("soyo", "长崎素世", typeof(SoyoAidHbrgoSaki)),
        new("mutsumi", "若叶睦", typeof(HalfBodyHbrgoSaki)),
        new("ruka", "茅森月歌", typeof(MyLegendBeginsHbrgoSaki)),
        new("yuki", "和泉由希", typeof(HelloWorldHbrgoSaki)),
        new("megumi", "逢川惠", typeof(SaviorGrandEntranceHbrgoSaki)),
        new("tama", "国见玉", typeof(WorldsFinestHbrgoSaki)),
        new("tsukasa", "东城司", typeof(MoreThanOneTruthHbrgoSaki)),
        new("karen", "朝仓可怜", typeof(CrushYouLikeABugHbrgoSaki)),
        new("tezuka_saki", "手冢咲", typeof(SummerGrassDreamHbrgoSaki)),
        new("nanami", "七濑七海", typeof(BoostUpHbrgoSaki))
    };

    // Direct acquisition and copies are authorized per instance, never by card type.
    private static readonly AsyncLocal<CardModel> AuthorizedCard = new();
    private static readonly ConditionalWeakTable<CardModel, Player> AuthorizedCopies = new();

    internal static bool CanAccess(Player player) =>
        player?.Character is HbrgoSaki31O or HbrgoSaki30A or HbrgoHotaru &&
        player.GetRelic<ElectronicSoldierManual>() != null;

    internal static void RecordEncounters(Player player, IEnumerable<string> characterIds)
    {
        if (player?.GetRelic<ElectronicSoldierManual>() is not { } manual || characterIds == null)
            return;

        HashSet<string> encountered = ReadEncounters(manual.MemoryGardenMetCharacters);
        foreach (string id in characterIds)
        {
            // Keep encounters even before that character has a garden card.
            if (HbrStoryCharacterCatalog.IsKnown(id))
                encountered.Add(id);
        }

        string updated = string.Join(",", encountered.OrderBy(id => id, StringComparer.Ordinal));
        if (updated == manual.MemoryGardenMetCharacters) return;
        manual.SetMemoryGardenEncounters(updated);
        Log.Info($"[HBRGOSakiMod][MemoryGarden] Encounter record updated: " +
            $"manual={manual.Id}, characters=[{updated}]");
    }

    private static HashSet<string> ReadEncounters(string stored) => new(
        (stored ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(HbrStoryCharacterCatalog.IsKnown),
        StringComparer.Ordinal);

    internal static IReadOnlyList<MemoryGardenChoice> GetChoices(Player player)
    {
        if (!CanAccess(player)) return Array.Empty<MemoryGardenChoice>();
        HbrMemoryGardenProgression.Synchronize(player);
        ElectronicSoldierManual manual = player.GetRelic<ElectronicSoldierManual>();
        manual.SynchronizeMemoryGardenUpgradePoints();
        bool hasUpgradePoint = manual.MemoryGardenUpgradePoints > 0;

        HashSet<string> encountered = ReadEncounters(
            player.GetRelic<ElectronicSoldierManual>().MemoryGardenMetCharacters);
        List<MemoryGardenChoice> choices = new();
        foreach (CharacterEntry character in Characters.Where(entry => encountered.Contains(entry.Id)))
        {
            CardModel canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(character.CardType));
            CardModel owned = FindOwnedCard(player, canonical.Id);
            bool isUpgrade = owned != null;
            bool hasEffect = owned == null || HbrMemoryGardenUpgradeCatalog.GetAvailableEffectIds(owned).Count > 0;
            bool available = hasUpgradePoint && hasEffect;
            // Previews never enter a card scope. New cards read the next six-sense
            // result from an independent RNG snapshot; owned cards keep theirs.
            CardModel preview = owned == null
                ? canonical.ToMutable()
                : (CardModel)owned.ClonePreservingMutability();
            if (preview.Owner == null) preview.Owner = player;
            if (owned == null) HbrMemoryGardenProgression.SynchronizePreview(preview, player);
            if (owned == null) SixSenseCardCapability.PreviewIfMissing(preview, player);
            string status = !hasEffect ? "已获得全部升级效果"
                : !hasUpgradePoint ? "升级点不足；获得或升级需要1个升级点。"
                : owned == null ? "消耗1个升级点获得这张卡牌。"
                : $"已获得 {owned.CurrentUpgradeLevel}/{owned.MaxUpgradeLevel} 种升级效果；消耗1个升级点选择一种新效果。";
            choices.Add(new(preview, character.Name, isUpgrade, available, status));
        }
        return choices;
    }

    private static CardModel FindOwnedCard(Player player, ModelId id) => player.Deck.Cards
        .Where(card => card.Id == id)
        .OrderBy(card => card.IsUpgradable ? 0 : 1)
        .ThenBy(card => card.CurrentUpgradeLevel)
        .FirstOrDefault();

    internal static async Task<bool> Recall(Player player)
    {
        if (!CanAccess(player) || player.RunState.CurrentRoom is not RestSiteRoom)
            return false;
        ElectronicSoldierManual manual = player.GetRelic<ElectronicSoldierManual>();
        manual.SynchronizeMemoryGardenUpgradePoints();
        if (manual.MemoryGardenUpgradePoints <= 0) return false;

        MemoryGardenSelectionScope scope = MemoryGardenSelectionScope.Capture(player);
        IReadOnlyList<MemoryGardenChoice> choices = GetChoices(player);
        Log.Info($"[HBRGOSakiMod][MemoryGarden] Open: manual={manual.Id}, " +
            $"characters=[{manual.MemoryGardenMetCharacters}], cards={choices.Count}");
        uint choiceId = scope.Synchronizer.ReserveChoiceId(player);
        ModelId selectedId;
        if (LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != NetGameType.Replay)
        {
            selectedId = TestMode.IsOn
                ? choices.FirstOrDefault(choice => choice.IsAvailable)?.PreviewCard.Id
                : await NMemoryGardenScreen.Choose(choices, manual.MemoryGardenUpgradePoints);
            scope.ThrowIfInvalid();
            // Synchronize cancellation too; remote players must not wait forever.
            CardModel canonical = choices.Any(choice => choice.IsAvailable && choice.PreviewCard.Id == selectedId)
                ? ModelDb.GetById<CardModel>(selectedId)
                : null;
            scope.Synchronizer.SyncLocalChoice(
                player, choiceId, PlayerChoiceResult.FromCanonicalCard(canonical));
            selectedId = canonical?.Id;
        }
        else
        {
            selectedId = (await scope.Synchronizer
                .WaitForRemoteChoice(player, choiceId)).AsCanonicalCard()?.Id;
        }

        scope.ThrowIfInvalid();
        bool claimed = await Claim(player, selectedId);
        scope.ThrowIfInvalid();
        return claimed;
    }

    internal static async Task<bool> Claim(Player player, ModelId cardId)
    {
        if (cardId == null || !CanAccess(player) || player.RunState.CurrentRoom is not RestSiteRoom)
            return false;
        ElectronicSoldierManual manual = player.GetRelic<ElectronicSoldierManual>();
        manual.SynchronizeMemoryGardenUpgradePoints();
        if (manual.MemoryGardenUpgradePoints <= 0) return false;
        // Deck-add hooks can await a free collection reward. Never await this
        // transaction from a nested purchase or serialize it into another run.
        if (!MemoryGardenUpgradePointsHelper.TryBeginPurchase(manual, out IDisposable purchase)) return false;
        using (purchase) return await ClaimWithUpgradePoint(player, cardId, manual);
    }

    private static async Task<bool> ClaimWithUpgradePoint(Player player, ModelId cardId,
        ElectronicSoldierManual manual)
    {

        MemoryGardenSelectionScope scope = MemoryGardenSelectionScope.Capture(player);
        // Validate again after the screen closes: neither a stale nor an injected
        // selection may grant an unmet character's card or exceed its upgrade limit.
        MemoryGardenChoice choice = GetChoices(player).FirstOrDefault(entry =>
            entry.PreviewCard.Id == cardId && entry.IsAvailable);
        if (choice == null) return false;

        CardModel owned = FindOwnedCard(player, cardId);
        if (owned != null)
        {
            bool upgraded = await ChooseUpgrade(owned, () => CanAccess(player) &&
                ReferenceEquals(player.GetRelic<ElectronicSoldierManual>(), manual) &&
                manual.MemoryGardenUpgradePoints > 0 && player.RunState.CurrentRoom is RestSiteRoom,
                "upgradePointNotice", (card, effectId) => PurchaseUpgrade(manual, card, effectId));
            scope.ThrowIfInvalid();
            return upgraded;
        }

        CardModel granted = player.RunState.CreateCard(ModelDb.GetById<CardModel>(cardId), player);
        if (!manual.TrySpendMemoryGardenUpgradePoint())
        {
            if (!granted.HasBeenRemovedFromState) granted.RemoveFromState();
            return false;
        }
        bool acquired = false;
        CardModel previousPermit = AuthorizedCard.Value;
        AuthorizedCard.Value = granted;
        try
        {
            CardPileAddResult result = await CardPileCmd.Add(granted, PileType.Deck);
            acquired = result.success;
            scope.ThrowIfInvalid();
            if (!result.success && granted.Pile == null && !granted.HasBeenRemovedFromState)
                granted.RemoveFromState();
            return result.success;
        }
        finally
        {
            AuthorizedCard.Value = previousPermit;
            // The card may have reached Deck before a reward callback cancels
            // the selection. Keep that real purchase paid; refund failed adds.
            acquired |= ReferenceEquals(granted.Owner, player) &&
                player.Deck.Cards.Any(card => ReferenceEquals(card, granted));
            if (!acquired)
            {
                if (granted.Pile == null && !granted.HasBeenRemovedFromState) granted.RemoveFromState();
                manual.RefundMemoryGardenUpgradePoint();
            }
        }
    }

    private static bool PurchaseUpgrade(ElectronicSoldierManual manual, CardModel card, int effectId)
    {
        if (!ReferenceEquals(card.Owner?.GetRelic<ElectronicSoldierManual>(), manual) ||
            !manual.TrySpendMemoryGardenUpgradePoint()) return false;
        int previousLevel = card.CurrentUpgradeLevel;
        try { return HbrMemoryGardenUpgradeCatalog.TryApplyUpgrade(card, effectId); }
        finally
        {
            // A synchronous publication can throw after changing the card.
            // Only refund a purchase whose upgrade did not actually happen.
            if (card.CurrentUpgradeLevel <= previousLevel) manual.RefundMemoryGardenUpgradePoint();
        }
    }

    // The same choice protocol serves rest-site upgrades and 30A collection
    // rewards. Keep RNG advancement and stable effect IDs identical on peers.
    internal static async Task<bool> ChooseUpgrade(CardModel card, Func<bool> canApply,
        string noticeKey = "upgradeNotice", Func<CardModel, int, bool> commitUpgrade = null)
    {
        if (card?.IsMutable != true || card.Owner is not { } player) return false;
        MemoryGardenSelectionScope scope = MemoryGardenSelectionScope.Capture(player);
        if (!canApply()) return false;
        int previousLevel = card.CurrentUpgradeLevel;
        AbstractRoom originalRoom = player.RunState.CurrentRoom;
        IReadOnlyList<MemoryGardenUpgradeOption> offers =
            HbrMemoryGardenUpgradeCatalog.RollOffers(card, player.PlayerRng.Rewards);
        if (offers.Count == 0) return false;

        uint choiceId = scope.Synchronizer.ReserveChoiceId(player);
        int effectId;
        if (LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != NetGameType.Replay)
        {
            effectId = TestMode.IsOn ? offers[0].EffectId
                : await NMemoryGardenUpgradeScreen.Choose(card.Title, offers, noticeKey);
            scope.ThrowIfInvalid();
            scope.Synchronizer.SyncLocalChoice(player, choiceId,
                PlayerChoiceResult.FromIndex(effectId >= 0 ? effectId : null));
        }
        else
        {
            effectId = (await scope.Synchronizer
                .WaitForRemoteChoice(player, choiceId)).AsIndexOrNull() ?? -1;
        }

        scope.ThrowIfInvalid();
        if (!canApply() || !ReferenceEquals(player.RunState.CurrentRoom, originalRoom) ||
            !ReferenceEquals(card.Owner, player) || card.HasBeenRemovedFromState || card.Pile?.Type != PileType.Deck ||
            !player.Deck.Cards.Any(ownedCard => ReferenceEquals(ownedCard, card)) ||
            card.CurrentUpgradeLevel != previousLevel || !offers.Any(offer => offer.EffectId == effectId))
            return false;
        return commitUpgrade == null
            ? HbrMemoryGardenUpgradeCatalog.TryApplyUpgrade(card, effectId)
            : commitUpgrade(card, effectId);
    }

    internal static void AuthorizeCopy(CardModel original, CardModel copy)
    {
        if (original?.IsMutable != true || copy?.IsMutable != true ||
            !HbrMemoryGardenCardCatalog.IsGardenCard(original) ||
            original.GetType() != copy.GetType() || original.Owner is not { } player ||
            !ReferenceEquals(copy.Owner, player)) return;

        // Real owned cards, a pending garden reward, and its copies can be
        // duplicated. Cloning an unclaimed reward/template does not grant it.
        if (original.Pile == null && !IsAcquisitionAuthorized(player, original)) return;
        AuthorizedCopies.AddOrUpdate(copy, player);
    }

    internal static void AuthorizeTheftReturn(Player player, CardModel card)
    {
        // Native theft restores this exact card to the run before offering it
        // as a reward. Rehydrated theft rewards do the same after loading.
        // Its upgrades/enchantment remain on the original/restored instance.
        if (card?.IsMutable != true || player == null || !ReferenceEquals(card.Owner, player) ||
            !HbrMemoryGardenCardCatalog.IsGardenCard(card) || card.HasBeenRemovedFromState ||
            card.Pile != null || player.RunState?.ContainsCard(card) != true) return;
        AuthorizedCopies.AddOrUpdate(card, player);
    }

    internal static bool IsAcquisitionAuthorized(Player player, CardModel card) =>
        card?.IsMutable == true && ReferenceEquals(card.Owner, player) &&
        (ReferenceEquals(AuthorizedCard.Value, card) ||
            (AuthorizedCopies.TryGetValue(card, out Player owner) && ReferenceEquals(owner, player)));
}
