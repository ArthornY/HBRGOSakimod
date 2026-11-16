using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

// Real attack-intent detection, swordheart spending, live attributes and
// retaliation. Zero-swordheart cases isolate only finisher presentation and
// the ready-vote request; the real magic-sword removal still executes.
static partial class EffectChecks
{
    static int SakuraFinished;
    static bool SakuraSkipOpening(ref Task<bool> __result) { __result = Task.FromResult(false); return false; }
    static bool SakuraFinishLeaf(ref Task<bool> __result) { SakuraFinished++; __result = Task.FromResult(true); return false; }
    static bool SakuraSkipTurnEnd() => false;
    static bool SakuraWaitLeaf(ref Task __result) { __result = Task.CompletedTask; return false; }

    static void SakuraIntent(bool attack) => Set(Enemy.Monster, "<NextMove>k__BackingField", attack
        ? new MoveState("SAKURA_ATTACK", _ => Task.CompletedTask, new SingleAttackIntent(() => 6m))
        : new MoveState("SAKURA_IDLE", _ => Task.CompletedTask));

    static async Task<SakuraRainPower> SakuraCard(bool upgraded = false)
    {
        await UnifiedPlayCard(Owned<SakuraRainHbrgoSaki>(), upgraded);
        return Hero.GetPower<SakuraRainPower>()!;
    }

    static async Task SakuraRainCases()
    {
        if (!ModelDb.Contains(typeof(SakuraRainPower)))
        {
            ModelDb.Inject(typeof(SakuraRainPower));
            Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(typeof(SakuraRainPower))), "<Id>k__BackingField", ModelDb.GetId(typeof(SakuraRainPower)));
        }
        var context = new ThrowingPlayerChoiceContext();
        bool wasInProgress = CombatManager.Instance.IsInProgress;
        var harmony = new Harmony(ProductionId + ".sakura-finisher-leaf");
        MethodInfo opening = typeof(MagicSwordModePower).GetMethod("PlayMagicSwordLuckySevenOpening", F)!;
        MethodInfo finisher = typeof(MagicSwordModePower).GetMethod("PlayMagicSwordLuckySeven", F)!;
        MethodInfo request = Helper("MagicSwordTurnEndHelper").GetMethod("Request", F)!;
        MethodInfo skipOpening = typeof(EffectChecks).GetMethod(nameof(SakuraSkipOpening), F)!;
        MethodInfo finishLeaf = typeof(EffectChecks).GetMethod(nameof(SakuraFinishLeaf), F)!;
        MethodInfo skipEnd = typeof(EffectChecks).GetMethod(nameof(SakuraSkipTurnEnd), F)!;
        MethodInfo waitLeaf = typeof(EffectChecks).GetMethod(nameof(SakuraWaitLeaf), F)!;
        MethodInfo[] waits = typeof(Cmd).GetMethods(F).Where(method =>
            method.Name == "CustomScaledWait" && method.ReturnType == typeof(Task)).ToArray();
        try
        {
            // Earlier card-only cases intentionally leave combat inactive.
            // Native Hook suppresses every combat listener in that state.
            CombatInProgressWithTurn();
            foreach (MethodInfo wait in waits) harmony.Patch(wait, prefix: new HarmonyMethod(waitLeaf));
            harmony.Patch(opening, prefix: new HarmonyMethod(skipOpening));
            harmony.Patch(finisher, prefix: new HarmonyMethod(finishLeaf));
            harmony.Patch(request, prefix: new HarmonyMethod(skipEnd));
            foreach (bool hbr in new[] { true, false })
            {
                Reset(hbr); As31O(true); SakuraIntent(true);
                var swordheart = Power<SwordheartPower>(Hero, 3);
                SakuraRainPower sakura = await SakuraCard();
                await sakura.BeforeSideTurnEnd(context, Hero.Side, new[] { Hero });
                Check(!sakura.IsDamageReductionActive && swordheart.Amount == 3,
                    "Sakura outside magic sword neither activates nor spends swordheart (HBR " + hbr + ")");
                var mode = Power<MagicSwordModePower>(Hero, 1);
                SakuraIntent(false);
                await sakura.BeforeSideTurnEnd(context, Hero.Side, new[] { Hero });
                Check(!sakura.IsDamageReductionActive && swordheart.Amount == 3,
                    "Sakura with no enemy attack intent neither activates nor spends swordheart");
                SakuraIntent(true);
                decimal before = Actual(Hero, Enemy, 6m, ValueProp.Move, null);
                await sakura.BeforeSideTurnEnd(context, Hero.Side, new[] { Hero });
                Check(sakura.IsDamageReductionActive && sakura.ActiveCopies == 1 && swordheart.Amount == 2 && mode.ConsumedSwordheartAmount == 1,
                    "one Sakura trigger pays one swordheart through the actual magic-sword consumption path");
                decimal after = Actual(Hero, Enemy, 6m, ValueProp.Move, null);
                Equal(Preview(Hero, Enemy, 6m, ValueProp.Move, null), after,
                    "active Sakura incoming-damage preview matches the actual damage boundary");
                if (hbr)
                {
                    Equal(HeroStat("Vitality"), 120m, "HBR Sakura raises live vitality from 100 to 120");
                    Equal(HeroStat("Spirit"), 120m, "HBR Sakura raises live spirit from 100 to 120");
                    Check(after < before, "Sakura's live defensive attributes reduce actual HBR incoming damage");
                    Equal(sakura.ModifyDamageMultiplicative(Hero, 6m, ValueProp.Move, Enemy, null!), 1m,
                        "HBR Sakura does not also apply the old half-damage modifier");
                }
                else Equal(after, before * .5m, "legacy Sakura retains the original half-damage calculation");
                await sakura.AfterPlayerTurnStart(context, Player);
                await sakura.AfterSideTurnEnd(context, Hero.Side, new[] { Hero });
                Check(sakura.IsDamageReductionActive, "Sakura does not expire at an intervening player boundary");
                await sakura.AfterSideTurnEnd(context, Enemy.Side, new[] { Enemy });
                Check(!sakura.IsDamageReductionActive && sakura.ActiveCopies == 0,
                    "Sakura expires at the enemy side's actual end");
                Equal(Actual(Hero, Enemy, 6m, ValueProp.Move, null), before,
                    "after enemy-side expiry incoming damage returns to its prior value");
            }

            Reset(); As31O(true); SakuraIntent(true);
            Power<SwordheartPower>(Hero, 4); Power<MagicSwordModePower>(Hero, 1);
            SakuraRainPower doubled = await SakuraCard();
            await SakuraCard();
            Check(doubled.AppliedCopies == 2, "two real Sakura card plays store two independent contributions in one ability");
            await doubled.BeforeSideTurnEnd(context, Hero.Side, new[] { Hero });
            Equal(HeroStat("Vitality"), 140m, "two Sakura cards add their vitality bonuses to 40 percent");
            Equal(HeroStat("Spirit"), 140m, "two Sakura cards add their spirit bonuses to 40 percent");
            Equal(Hero.GetPowerAmount<SwordheartPower>(), 3m, "two Sakura contributions still pay the existing one-swordheart trigger cost");
            var doubler = Owned<DoublePowersHbrgoSaki>();
            await (Task)Native(doubler, "OnPlay", context, Play(doubler))!;
            Check(doubled.AppliedCopies == 2 && doubled.ActiveCopies == 2,
                "doubling native layers does not invent additional played Sakura cards");

            foreach (bool clarity in new[] { false, true })
            {
                Reset(); As31O(true); SakuraIntent(true); SakuraFinished = 0;
                Power<SwordheartPower>(Hero, clarity ? 2 : 1);
                var mode = Power<MagicSwordModePower>(Hero, 1);
                if (clarity) Power<SwordheartClarityPower>(Hero, 1);
                SakuraRainPower sakura = await SakuraCard();
                await sakura.BeforeSideTurnEnd(context, Hero.Side, new[] { Hero });
                Check(!Hero.HasPower<MagicSwordModePower>() && Hero.GetPowerAmount<SwordheartPower>() == 0 && SakuraFinished == 1,
                    "Sakura exhausting its payment closes magic sword through the existing normal-exit path: mode=" +
                    Hero.HasPower<MagicSwordModePower>() + ", sword=" + Hero.GetPowerAmount<SwordheartPower>() +
                    ", finishers=" + SakuraFinished + ", consumed=" + mode.ConsumedSwordheartAmount + ", clarity=" + clarity);
                Equal(mode.ConsumedSwordheartAmount, clarity ? 2m : 1m,
                    "Sakura payment respects swordheart clarity's double cost and finisher accounting");
                Equal(HeroStat("Vitality"), 120m, "Sakura still grants vitality after its payment closes magic sword");
                Equal(HeroStat("Spirit"), 120m, "Sakura still grants spirit after its payment closes magic sword");
            }

            Reset(); As31O(true); SakuraIntent(true);
            Power<SwordheartPower>(Hero, 4); Power<MagicSwordModePower>(Hero, 1);
            SakuraRainPower inserted = await SakuraCard();
            object token = new();
            await (Task)Invoke(typeof(SakuraRainPower).GetMethod("TriggerInsertedTurnEnd", F)!, null, context, Player, token)!;
            await (Task)Invoke(typeof(SakuraRainPower).GetMethod("TriggerInsertedTurnEnd", F)!, null, context, Player, token)!;
            Equal(Hero.GetPowerAmount<SwordheartPower>(), 3m, "one inserted-turn token cannot trigger Sakura twice");
            await (Task)Invoke(typeof(SakuraRainPower).GetMethod("TriggerInsertedTurnEnd", F)!, null, context, Player, new object())!;
            Equal(Hero.GetPowerAmount<SwordheartPower>(), 2m, "a separate inserted turn can trigger Sakura again");
            Equal(HeroStat("Vitality"), 120m, "another trigger refreshes the same played-copy bonus instead of compounding it");

            Reset(); As31O(true); SakuraIntent(true);
            Power<SwordheartPower>(Hero, 3); Power<MagicSwordModePower>(Hero, 1);
            SakuraRainPower retaliate = await SakuraCard(upgraded: true);
            await retaliate.BeforeSideTurnEnd(context, Hero.Side, new[] { Hero });
            var second = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!;
            Set(Combat, "_enemies", new List<Creature> { Enemy, second });
            var result = Blank<DamageResult>(); Set(result, "<Receiver>k__BackingField", Hero); Set(result, "<UnblockedDamage>k__BackingField", 100);
            int rng = RngCount(RunState.Rng.Niche);
            Hits.Clear();
            await retaliate.AfterDamageReceived(context, Hero, result, ValueProp.Move, Enemy, null!);
            Check(Hits.Count == 2 && Hits.All(hit => hit.Raw == 50m && hit.Props == ValueProp.Unpowered && hit.Enabled && hit.Final < 50m),
                "upgraded Sakura retains half the received damage as one world-space skill basis, applies actor attributes, and preserves native Unpowered recursion flags");
            Equal(RngCount(RunState.Rng.Niche), rng + 1, "Sakura's world-space skill samples one shared natural-critical roll for its whole AoE command");
            Check(Hits.All(hit => hit.Sample.HasValue) && Hits.Select(hit => hit.Sample).Distinct().Count() == 1,
                "Sakura retaliation shares its one critical sample across every enemy");
            await retaliate.AfterSideTurnEnd(context, Enemy.Side, new[] { Enemy, second });
            Hits.Clear();
            await retaliate.AfterDamageReceived(context, Hero, result, ValueProp.Move, Enemy, null!);
            Check(Hits.Count == 0, "the retaliation upgrade expires with Sakura's defensive interval");
        }
        finally
        {
            harmony.Unpatch(opening, skipOpening); harmony.Unpatch(finisher, finishLeaf); harmony.Unpatch(request, skipEnd);
            foreach (MethodInfo wait in waits) harmony.Unpatch(wait, waitLeaf);
            CombatInProgress(wasInProgress);
            SakuraFinished = 0; As31O(false); Reset();
        }
    }
}
