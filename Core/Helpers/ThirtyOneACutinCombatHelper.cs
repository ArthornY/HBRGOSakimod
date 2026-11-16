using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyOneACutinCombatHelper
{
    private sealed class TurnMark { internal object Combat; internal int Turn = -1; }
    private static readonly ConditionalWeakTable<Player, TurnMark> Turns = new();
    internal static int CutinCount(int kinds) => kinds >= 6 ? 6 : kinds >= 4 ? 3 : kinds >= 2 ? 2 : 0;

    internal static string Character(CardModel card) => card switch
    {
        MyLegendBeginsHbrgoSaki => "RKayamori", HelloWorldHbrgoSaki => "YIzumi",
        SaviorGrandEntranceHbrgoSaki => "MAikawa", MoreThanOneTruthHbrgoSaki => "TTojo",
        CrushYouLikeABugHbrgoSaki => "Karen", WorldsFinestHbrgoSaki => "TKunimi", _ => null
    };

    internal static IEnumerable<IHoverTip> HoverTips() => new IHoverTip[]
    {
        new HoverTip(HbrCompactText.Literal("title", "31A 插嘴"), HbrCompactText.Literal("text", "每个正常回合抽牌后，随机由牌组中的队员插嘴。额外回合不触发；接话只作演出。")),
        new HoverTip(HbrCompactText.Literal("title", "由希的预告"), HbrCompactText.Literal("text", "下一次施加的减益数值提高30%。同时施加给多个目标只消耗一次；多个预告分别生效一次，保留至战斗结束。"))
    };

    internal static async Task TurnStart(PlayerChoiceContext context, ThirtyOneACollection relic)
    {
        Player player = relic.Owner;
        if (player?.Creature is not { IsAlive: true, CombatState: { } combat } owner ||
            OverclockImmediateTurnCoordinator.IsActiveFor(player) || PersonalImmediateTurnCoordinator.IsActiveFor(player) ||
            CombatManager.Instance.PlayersTakingExtraTurn.Contains(player)) return;
        int turn = player.PlayerCombatState?.TurnNumber ?? 0;
        TurnMark mark = Turns.GetOrCreateValue(player);
        if (ReferenceEquals(mark.Combat, combat) && mark.Turn == turn) return;
        mark.Combat = combat; mark.Turn = turn;
        ThirtyOneACutinConditionHelper.Observe(owner);
        List<string> candidates = player.Deck.Cards.Where(card => card.Owner == player && !card.HasBeenRemovedFromState)
            .Select(Character).Where(character => character != null).Distinct().OrderBy(character => character, StringComparer.Ordinal).ToList();
        int count = CutinCount(candidates.Count);
        for (int i = 0; i < count && owner.IsAlive && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            int index = player.RunState.Rng.CombatTargets.NextInt(0, candidates.Count);
            string character = candidates[index]; candidates.RemoveAt(index);
            int roll = 0; string role = null;
            relic.Flash();
            switch (character)
            {
                case "RKayamori": await PowerCmd.Apply<HbrMoralePower>(context, owner, 1m, owner, null); break;
                case "YIzumi": await PowerCmd.Apply<ThirtyOneAYukiDebuffPower>(context, owner, 1m, owner, null); break;
                case "MAikawa":
                    bool success = player.RunState.Rng.CombatTargets.NextInt(0, 100) < 25;
                    role = success ? "中了" : "没中";
                    Creature[] enemies = combat.GetOpponentsOf(owner).Where(enemy => enemy.IsAlive && enemy.Monster != null).ToArray();
                    if (success && enemies.Length > 0)
                    {
                        Creature target = enemies[player.RunState.Rng.CombatTargets.NextInt(0, enemies.Length)];
                        await CreatureCmd.Stun(target);
                        await SaviorGrandEntranceCombatHelper.OnEnemyStunned(context, owner, target);
                    }
                    break;
                case "TTojo":
                    roll = player.RunState.Rng.CombatTargets.NextInt(1, 101); role = roll >= 50 ? "中了" : "没中";
                    await ApplyTsukasa(context, owner, roll);
                    break;
                case "Karen":
                    roll = player.RunState.Rng.CombatTargets.NextInt(1, 101);
                    await HbrEffectApplication.Apply<HbrAttackUpPower>(context, owner, roll, owner, null,
                        profile: new(HbrEffectLimit.Default, HbrEffectExit.PlayerTurnEnd, 1));
                    break;
                case "TKunimi":
                    await HbrMatchaDpHelper.Restore(context, player, .05m, convertOverflow: false);
                    await CardPileCmd.Draw(context, 2m, player);
                    break;
            }
            Show(player, character, role, roll);
        }
    }

    private static async Task ApplyTsukasa(PlayerChoiceContext context, Creature owner, int roll)
    {
        using IDisposable batch = HbrDebuffApplicationScope.BeginBatch(owner);
        foreach (Creature enemy in owner.CombatState.GetOpponentsOf(owner).Where(enemy => enemy.IsAlive).ToArray())
            await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy, roll, owner,
                null, new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
    }

    internal static void Show(Player player, string character, string role = null, int roll = 0, bool live = false) =>
        NThirtyOneACutinOverlay.Enqueue(player, character, role, roll, live);
}
