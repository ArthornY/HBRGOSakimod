using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruClassRelicCombatHelper
{
    private sealed class BreakReceipt { internal ICombatState Combat; internal bool Pending; internal bool Resolving; }
    private static readonly ConditionalWeakTable<Player, BreakReceipt> Breaks = new();
    internal static bool IsEnabled(Player player) => player?.Character is HbrgoHotaru && HbrBattleStatsHelper.IsEnabled(player);
    private static T Active<T>(Player player) where T : HotaruClassRelic =>
        player?.GetRelic<T>() is { IsMelted: false } relic && IsEnabled(player) ? relic : null;

    internal static void ResetBattle(Player owner)
    {
        if (owner == null) return;
        Breaks.Remove(owner);
        owner.GetRelic<CheersByTheSea>()?.ResetBattle();
        owner.GetRelic<SmilingPanKeychain>()?.ResetBattle();
        owner.GetRelic<StreetPiano>()?.ResetBattle();
    }

    // Called only after Millsage has summoned the non-retired cannon roster.
    internal static async Task OnCannonsReady(Millsage millsage)
    {
        Player player = millsage.Owner;
        if (!IsEnabled(player)) return;
        var context = new BlockingPlayerChoiceContext();
        if (Active<TheLastStage>(player) is { } stage)
        {
            stage.Flash();
            await millsage.TriggerAllCannons(context);
        }
        if (Active<StreetPiano>(player) is not { } piano || !HotaruCommonCannonHelper.CanAct(player)) return;
        Creature[] cannons = HotaruCommonCannonHelper.GetCannons(player);
        Creature selected = cannons.Length == 1 ? cannons[0] : cannons.Length > 1
            ? await HotaruCommonCannonHelper.SelectCannon(context, player) : null;
        if (!Millsage.IsOwnedCannon(selected, player)) return;
        piano.Select(selected, ((HotaruCannonModel)selected.Monster).Kind);
        piano.Flash();
        await millsage.TriggerAllEffects(selected, context);
    }

    // Millsage's successful phase guard deduplicates native/OD/personal entries.
    internal static async Task OnOwnTurnStart(PlayerChoiceContext context, Millsage millsage)
    {
        if (Active<StreetPiano>(millsage.Owner) is not { } piano) return;
        if (piano.AdvanceTurn(millsage) is not { } cannon) return;
        piano.Flash();
        await millsage.TriggerAllEffects(cannon, context);
    }

    internal static async Task OnActivation(PlayerChoiceContext context, Player owner)
    {
        if (Active<CheersByTheSea>(owner) is not { } relic || !relic.ClaimActivation()) return;
        // Claim before drawing: the drawn card may itself activate another cannon.
        relic.Flash();
        await CardPileCmd.Draw(context, 1m, owner);
        if (HotaruCommonCannonHelper.CanAct(owner))
            await PowerCmd.Apply<ChargePower>(context, owner.Creature, 1m, owner.Creature, null);
    }

    internal static Creature[] AddGiftCannon(Player owner, IReadOnlyList<Creature> handbookCannons,
        IReadOnlyList<Creature> available)
    {
        if (Active<GiftFromAbove>(owner) is not { } gift || available.Count == 0) return handbookCannons.ToArray();
        Creature[] other = available.Where(cannon => !handbookCannons.Contains(cannon)).ToArray();
        IReadOnlyList<Creature> candidates = other.Length > 0 ? other : available;
        Creature selected = candidates[owner.RunState.Rng.Niche.NextInt(candidates.Count)];
        gift.Flash();
        return handbookCannons.Concat(new[] { selected }).ToArray();
    }

    // Only Deflector.AbsorbDp's real damage path calls this (not costs/clamps).
    internal static void QueueDpBreak(Player owner)
    {
        if (Active<SmilingPanKeychain>(owner) is not { } relic || owner.Creature.CombatState is not { } combat) return;
        object extraTurn = OverclockImmediateTurnCoordinator.GetTurnToken(owner);
        string sideTurn = FormattableString.Invariant($"{combat.RoundNumber}:{combat.CurrentSide}:{owner.PlayerCombatState?.TurnNumber ?? 0}");
        if (!relic.ClaimBreak(sideTurn, extraTurn)) return;
        BreakReceipt receipt = Breaks.GetOrCreateValue(owner);
        receipt.Combat = combat;
        receipt.Pending = true;
    }

    // Awaited in the native per-hit callback, before the next hit can spend Block.
    internal static async Task ResolveDpBreak(PlayerChoiceContext context, Player owner)
    {
        if (owner == null || !Breaks.TryGetValue(owner, out BreakReceipt receipt) || !receipt.Pending || receipt.Resolving) return;
        receipt.Pending = false;
        if (!ReferenceEquals(receipt.Combat, owner.Creature.CombatState) || Active<SmilingPanKeychain>(owner) is not { } relic ||
            owner.GetRelic<Millsage>() is not { } millsage || !HotaruCommonCannonHelper.CanAct(owner)) return;
        receipt.Resolving = true;
        try { relic.Flash(); await millsage.TriggerAllCannons(context); }
        finally { receipt.Resolving = false; }
    }

    internal static IEnumerable<IHoverTip> HoverTips(HotaruClassRelic relic)
    {
        IHoverTip Tip(string id) => new HoverTip(new LocString("relics", "HotaruClassRelics." + id + ".title"),
            new LocString("relics", "HotaruClassRelics." + id + ".description"));
        if (relic is KanatasHomework) { yield return Tip("gardenPoint"); yield break; }
        yield return Tip("cannons");
        if (relic is CheersByTheSea or GiftFromAbove or StreetPiano) yield return Tip("activation");
        if (relic is TheLastStage or SmilingPanKeychain) yield return Tip("completeAction");
        if (relic is GiftFromAbove) yield return Tip("critical");
        if (relic is CheersByTheSea) yield return HoverTipFactory.FromPower<ChargePower>();
        if (relic is SmilingPanKeychain) yield return Tip("dp");
    }
}
