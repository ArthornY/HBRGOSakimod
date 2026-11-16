using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

public readonly record struct ThirtyGRunState(int San, int Popularity, int NextRoundStart, bool Broken);
public static class ThirtyGBondHelper
{
    private sealed class Battle
    {
        internal readonly HashSet<Player> Started = new();
        internal readonly Dictionary<Player, HashSet<(int, object)>> Turns = new();
    }
    private sealed class DischargeScope : IDisposable
    {
        internal Creature Owner;
        internal AttackCommand Command;
        internal DischargeScope Previous;
        internal bool Active = true;
        public void Dispose() { Active = false; if (ReferenceEquals(Discharging.Value, this)) Discharging.Value = Previous; }
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly AsyncLocal<DischargeScope> Discharging = new();
    private static readonly FieldInfo AttackerField = typeof(AttackCommand).GetField("<Attacker>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo SourceField = typeof(AttackCommand).GetField("<ModelSource>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    public static event Action<Player, ThirtyGRunState> RunChanged;
    public static ThirtyGRunState GetRunState(Player player) => player?.GetRelic<ThirtyGCollectionRelic>() is { } relic
        ? new(relic.San, relic.Popularity, relic.NextRoundStart, relic.San <= 0) : default;
    private static void Notify(ThirtyGCollectionRelic relic) => RunChanged?.Invoke(relic.Owner, GetRunState(relic.Owner));
    internal static void ResetSan(Player player)
    {
        if (player?.GetRelic<ThirtyGCollectionRelic>() is not { } relic) return;
        relic.SetSan(100); Notify(relic);
    }
    internal static void RoomEntered(Player player, AbstractRoom room)
    {
        if (player?.GetRelic<ThirtyGCollectionRelic>() is not { } relic) return;
        var data = ThirtyGProposalState.Read(relic);
        int act = player.RunState.CurrentActIndex;
        if (data.LastAct != act || room is RestSiteRoom) ResetSan(player);
        if (data.LastAct != act) { data.LastAct = act; ThirtyGProposalState.Save(relic, data); }
    }
    internal static async Task PrepareBattle(PlayerChoiceContext context, Player player)
    {
        if (player?.GetRelic<ThirtyGCollectionRelic>() is not { } relic || player.Creature?.CombatState is not { } combat ||
            !HbrBattleStatsHelper.IsEnabled(player)) return;
        Battle battle = Battles.GetOrCreateValue(combat);
        if (!battle.Started.Add(player)) return;
        ThirtyGProposalState.EnsureIdentities(player);
        var data = ThirtyGProposalState.Read(relic);
        if (data.LastAct != player.RunState.CurrentActIndex)
        { data.LastAct = player.RunState.CurrentActIndex; relic.SetSan(100); Notify(relic); }
        string key = player.RunState.TotalFloor + ":" + combat.Encounter?.Id.Entry;
        if (data.InBattle && data.BattleKey == key)
        { ThirtyGProposalState.NotifyOwner(player); return; }
        if (data.InBattle) EndBattle(player);
        data = ThirtyGProposalState.Read(relic); data.InBattle = true; data.BattleKey = key;
        foreach (var entry in data.Cards.Values)
        {
            entry.Released = false; entry.ReleaseSecondTurn = false;
            entry.Dressed = false; entry.ComboPending = false; entry.DressPride = false;
            entry.LastDressRoll = 0; entry.LastDressSuccess = false;
        }
        ThirtyGProposalState.Save(relic, data);
        ThirtyGProposalState.NotifyOwner(player);
        await Task.CompletedTask;
    }
    private static async Task Propose(PlayerChoiceContext context, Player player, ThirtyGCollectionRelic relic)
    {
        var data = ThirtyGProposalState.Read(relic);
        CardModel[] candidates = (player.PlayerCombatState?.AllPiles.SelectMany(pile => pile.Cards) ?? Enumerable.Empty<CardModel>())
            .Where(card => card.Owner == player && !card.HasBeenRemovedFromState && HbrMemoryGardenCardCatalog.IsGardenCard(card) &&
                card is not AllOrdersConfirmedHbrgoSaki && card is not IThirtyGMemberCard { ThirtyGMember: ThirtyGMember.ShirakawaYuina } &&
                HbrMemoryGarden.CharacterIdOf(card) != "yuina" && !ThirtyGProposalState.Get(card).Graduated && ThirtyGProposalState.Get(card).HeartfeltExtraPlays == 0)
            .Distinct().ToArray();
        int kinds = relic.Kinds;
        CardModel target = null;
        int reaction = 0, sanRoll = 0, sanAfter = relic.San;
        bool vote = false;
        if (kinds >= 2 && relic.San > 0 && candidates.Length > 0)
        {
            Rng rng = RngCompatibility.CreateNamed(player.RunState.Rng, "hbrgo_30g_proposal_" + player.NetId + "_" + data.ProposalSerial);
            target = await SelectProposalTarget(context, player, candidates, rng);
            if (target != null)
            {
                string id = ThirtyGProposalState.EnsureIdentity(target, relic);
                bool miya = target is IThirtyGMemberCard { ThirtyGMember: ThirtyGMember.KiryuMiya } || HbrMemoryGarden.CharacterIdOf(target) == "miya";
                reaction = kinds >= 6 ? rng.NextInt(1, 4) : 0;
                sanRoll = rng.NextInt(1, 101); sanAfter = Math.Max(0, relic.San - sanRoll);
                data.ProposalSerial++;
                if (!data.Cards.TryGetValue(id, out var proposed)) data.Cards[id] = proposed = new();
                proposed.GraduatedRemaining = 1; proposed.Heart = 0; proposed.PendingHeart = 0;
                proposed.MiyaProposal = miya;
                proposed.ReleaseSecondTurn = false; proposed.Released = false;
                if (!data.RoundTargets.Contains(id)) data.RoundTargets.Add(id);
                relic.GainPopularity(); vote = kinds >= 4 && relic.Popularity >= 10;
                ThirtyGProposalState.Save(relic, data); ThirtyGProposalState.Notify(ThirtyGProposalState.Physical(target));
                NoblesseObligeCombatHelper.OnProposal(player, target);
                await ThirtyGProposalPresentationBridge.Present(new(player, target,
                    HbrMemoryGarden.CharacterIdOf(target) ?? target.Id.Entry, reaction, sanRoll, sanAfter, sanAfter <= 0, relic.Popularity, vote));
            }
        }
        if (target != null)
        {
            relic.SetSan(sanAfter); Notify(relic);
            if (reaction == 2) await Discharge(context, player);
            if (vote) await ResolveVote(context, relic);
        }
        ThirtyGProposalState.Save(relic, data); ThirtyGProposalState.NotifyOwner(player);
    }
    internal static async Task<CardModel> SelectProposalTarget(PlayerChoiceContext context, Player player,
        IReadOnlyList<CardModel> candidates, Rng rng)
    {
        if (!NoblesseObligeCombatHelper.HasMoonlight(player)) return candidates[rng.NextInt(candidates.Count)];
        var selected = await CardSelectCmd.FromSimpleGrid(context, candidates, player,
            new CardSelectorPrefs(new LocString("relics", "THIRTY_G_COLLECTION_RELIC.selectionPrompt"), 1));
        return selected.FirstOrDefault(card => card.Owner == player && candidates.Contains(card));
    }
    internal static async Task ResolveVote(PlayerChoiceContext context, ThirtyGCollectionRelic relic)
    {
        var data = ThirtyGProposalState.Read(relic);
        var cards = relic.Owner.Deck.Cards.Where(card => card.Owner == relic.Owner && !card.HasBeenRemovedFromState &&
            ThirtyGProposalState.IdentityOf(card) is { } id && data.RoundTargets.Contains(id))
            .Select(card => (Card: card, Level: card.CurrentUpgradeLevel)).ToArray();
        data.RoundTargets.Clear(); relic.FinishVote(); ThirtyGProposalState.Save(relic, data); Notify(relic);
        foreach (var (card, level) in cards)
            if (card.CurrentUpgradeLevel <= level && HbrMemoryGardenUpgradeCatalog.GetAvailableEffectIds(card).Count > 0)
                await HbrMemoryGarden.ChooseUpgrade(card, () => card.CurrentUpgradeLevel <= level && card.IsUpgradable, "marriageVoteNotice");
    }
    internal static async Task TurnStart(PlayerChoiceContext context, Player player)
    {
        if (player?.GetRelic<ThirtyGCollectionRelic>() is not { } relic || player.Creature?.CombatState is not { } combat ||
            !HbrBattleStatsHelper.IsEnabled(player)) return;
        await PrepareBattle(context, player);
        Battle battle = Battles.GetOrCreateValue(combat);
        if (!battle.Turns.TryGetValue(player, out var turns)) battle.Turns.Add(player, turns = new());
        if (!turns.Add((player.PlayerCombatState?.TurnNumber ?? 0, OverclockImmediateTurnCoordinator.GetTurnToken(player)))) return;
        await Propose(context, player, relic);
    }
    internal static void EndBattle(Player player)
    {
        if (player?.GetRelic<ThirtyGCollectionRelic>() is not { } relic) return;
        var data = ThirtyGProposalState.Read(relic); if (!data.InBattle) return;
        data.InBattle = false;
        foreach (var entry in data.Cards.Values)
        {
            if (entry.MiyaProposal)
            { entry.GraduatedRemaining = 0; entry.PermanentHeart = 1; entry.Heart = 0; entry.MiyaProposal = false; }
            entry.Released = false; entry.ReleaseSecondTurn = false;
            entry.Dressed = false; entry.ComboPending = false; entry.DressPride = false; entry.LastDressRoll = 0; entry.LastDressSuccess = false;
        }
        ThirtyGProposalState.Save(relic, data); ThirtyGProposalState.NotifyOwner(player);
    }
    private static async Task Discharge(PlayerChoiceContext context, Player player)
    {
        AttackCommand command = DamageCmd.Attack(HbrSkillPower.DamageUnits(51m));
        AttackerField.SetValue(command, player.Creature); SourceField.SetValue(command, player.GetRelic<ThirtyGCollectionRelic>());
        command.TargetingAllOpponents(player.Creature.CombatState).WithHitCount(3).WithNoAttackerAnim().WithHitFx("vfx/vfx_attack_lightning");
        using DischargeScope scope = new() { Owner = player.Creature, Command = command, Previous = Discharging.Value };
        Discharging.Value = scope; await command.Execute(context);
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props)
    {
        for (DischargeScope scope = Discharging.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Owner, dealer) && target != null && target.Side != dealer.Side &&
                props.IsPoweredAttack() && HighHitDamageVisualHelper.IsDirectAttackDamage(scope.Command))
                return 1.2m / HbrDestructionGainHelper.DefaultSkillCoefficient;
        return 1m;
    }
    // Yotsuha's side-end deduplication still queries this compatibility hook.
    // The new proposal rule never converts a hand-end callback into a play.
    internal static object GetEndRepeatToken(CardModel card) => null;
}
