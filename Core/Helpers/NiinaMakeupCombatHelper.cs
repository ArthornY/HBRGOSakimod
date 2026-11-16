using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class NiinaMakeupCombatHelper
{
    private sealed class Battle { internal readonly HashSet<Player> Started = new(), Lost = new(); }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly AsyncLocal<AttackScope> Current = new();
    internal sealed class AttackScope : IDisposable
    {
        internal Creature Dealer;
        internal CardModel Card;
        internal decimal Units;
        internal AttackScope Previous;
        internal bool Active = true;
        public void Dispose() { Active = false; RestoreCaller(this); }
    }
    internal static async Task OnCombatStart(ICombatState combat)
    {
        if (combat == null) return;
        Battle battle = Battles.GetOrCreateValue(combat);
        foreach (Player player in combat.Players)
        {
            if (!HbrBattleStatsHelper.IsEnabled(player) || !battle.Started.Add(player)) continue;
            var card = player.Deck?.Cards.OfType<NarcissismIsFineHbrgoSaki>().FirstOrDefault(card =>
                card.Owner == player && !card.HasBeenRemovedFromState && !ThirtyGProposalState.IsDisabled(card) && card.HasUpgradeEffect(NarcissismIsFineHbrgoSaki.Makeup));
            if (card == null || battle.Lost.Contains(player)) continue;
            player.GetRelic<DeflectorProtection>()?.EnsureDpInitialized();
            var dp = ReadDp(player);
            if (dp.Current <= 0m || dp.Maximum <= 0m) { battle.Lost.Add(player); continue; }
            if (!player.Creature.HasPower<NiinaMakeupPower>())
                await HbrEffectApplication.Apply<NiinaMakeupPower>(new ThrowingPlayerChoiceContext(), player.Creature,
                    30m, player.Creature, card);
        }
    }
    // Pure view: never initialize or refill a pool during a card preview.
    private static (decimal Current, decimal Maximum) ReadDp(Player player)
    {
        if (HbrCardEffectPreview.TryGetDp(player, out decimal current, out decimal maximum)) return (current, maximum);
        Creature target = FubukiChangeCombatHelper.DpTarget(player);
        if (target?.Player != null)
        {
            var relic = player.GetRelic<DeflectorProtection>();
            return (relic?.Dp ?? 0, relic?.MaxDp ?? 0);
        }
        var shell = target?.GetPower<HighDimensionalShellPower>();
        return (shell?.CurrentDp ?? 0m, shell?.MaxAmount ?? 0m);
    }
    internal static void OnDpChanged(Creature changed)
    {
        Player player = changed?.Player ?? changed?.PetOwner;
        if (player?.Creature?.CombatState is not { } combat ||
            ReadDp(player).Current > 0m) return;
        Creature pool = FubukiChangeCombatHelper.DpTarget(player);
        if (!ReferenceEquals(changed, pool) && !(changed.PetOwner == player && ReferenceEquals(pool, player.Creature))) return;
        bool marker = player.Creature.HasPower<NiinaMakeupPower>();
        if (!Battles.TryGetValue(combat, out Battle battle))
        {
            if (!marker) return; // initial/save hydration before battle setup
            battle = Battles.GetOrCreateValue(combat);
        }
        if (!marker && !battle.Started.Contains(player)) return;
        battle.Lost.Add(player);
        // This is a synchronous resource invariant, so a cost that empties DP
        // removes the marker before its same card begins its attack.
        foreach (var power in player.Creature.Powers.OfType<NiinaMakeupPower>().ToArray()) power.RemoveInternal();
    }
    private static bool Applies(Creature dealer, CardModel source, ValueProp props) => source?.IsMutable == true &&
        source.Type == CardType.Attack && source.Owner?.Creature == dealer && HbrBattleStatsHelper.IsEnabled(dealer) &&
        props.IsPoweredAttack() && !HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source);
    private static decimal LiveUnits(Creature dealer)
    {
        if (dealer?.HasPower<NiinaMakeupPower>() != true || dealer.Player is not { } player) return 0m;
        var dp = ReadDp(player);
        return dp.Maximum > 0m && dp.Current > 0m ? HbrSkillPower.DamageUnits(30m * dp.Current / dp.Maximum) : 0m;
    }
    internal static decimal GetBonusUnits(Creature dealer, CardModel source, ValueProp props)
    {
        if (!Applies(dealer, source, props)) return 0m;
        if (HbrCardEffectPreview.IsMakeupSuppressed(dealer.Player)) return 0m;
        for (AttackScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Dealer, dealer) && ReferenceEquals(scope.Card, source)) return scope.Units;
        return LiveUnits(dealer);
    }
    internal static AttackScope BeginAttack(AttackCommand command)
    {
        if (command?.ModelSource is not CardModel card || !Applies(command.Attacker, card, command.DamageProps) ||
            !command.Attacker.HasPower<NiinaMakeupPower>()) return null;
        AttackScope scope = new() { Dealer = command.Attacker, Card = card, Units = LiveUnits(command.Attacker), Previous = Current.Value };
        Current.Value = scope; return scope;
    }
    internal static void RestoreCaller(AttackScope scope)
    { if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous; }
    internal static async Task<AttackCommand> Finish(Task<AttackCommand> native, AttackScope scope)
    { try { return await native; } finally { scope?.Dispose(); } }
}
