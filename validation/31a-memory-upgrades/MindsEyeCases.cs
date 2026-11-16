using System.Reflection;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static partial class EffectChecks
{
    static AttackCommand EyeAttack(CardModel? card = null)
    {
        var attack = new AttackCommand(6m);
        Set(attack, "<Attacker>k__BackingField", Hero);
        Set(attack, "<ModelSource>k__BackingField", card);
        Set(attack, "<DamageProps>k__BackingField", ValueProp.Move);
        return attack;
    }
    static async Task EyeResolveAttack(AttackCommand attack, int hits = 3)
    {
        await Hook.BeforeAttack(Combat, attack);
        for (int i = 0; i < hits; i++)
            attack.AddResultsInternal(new[] { new DamageResult(Enemy, ValueProp.Move) { BlockedDamage = 1 } });
        await Hook.AfterAttack(Combat, new ThrowingPlayerChoiceContext(), attack);
    }
    static decimal EyePercent(CardModel? card) => (decimal)Call("MindsEyeCombatHelper", "GetActualPercent", Hero, card)!;
    static decimal EyePotency(PowerModel power) => decimal.Parse(
        (string)power.GetType().GetProperty("HbrPotencyData", F)!.GetValue(power)!, System.Globalization.CultureInfo.InvariantCulture);
    static async Task MindsEyePersonalMembershipCases()
    {
        GardenReset(); var truth = GardenCard<MoreThanOneTruthHbrgoSaki>(3, 6); GardenPermanent(truth);
        var peer = SupportTeammate();
        var peerTruth = (MoreThanOneTruthHbrgoSaki)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(MoreThanOneTruthHbrgoSaki))).MutableClone();
        Set(peerTruth, "_owner", peer); GardenUpgrade(peerTruth, 3); GardenUpgrade(peerTruth, 6); peer.Deck.AddInternal(peerTruth);
        Set(RunState, "_players", new List<MegaCrit.Sts2.Core.Entities.Players.Player> { Player, peer });
        Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature });
        var saviorCard = GardenCard<SaviorGrandEntranceHbrgoSaki>(3); await GardenPlay(saviorCard);
        var savior = Hero.GetPower<SaviorGrandEntrancePower>()!;
        Check((bool)Native(savior, "TryTakeAllEnemyStun")! && !(bool)Native(savior, "TryTakeAllEnemyStun")!,
            "Savior's normal-turn all-enemy stun starts with exactly one use before interruption");
        Type coordinator = Helper("PersonalImmediateTurnCoordinator");
        FieldInfo current = coordinator.GetField("_current", F)!, combat = coordinator.GetField("_combat", F)!;
        object? oldCurrent = current.GetValue(null), oldCombat = combat.GetValue(null);
        Type sessionType = coordinator.GetNestedType("Session", F)!;
        object session = Activator.CreateInstance(sessionType, true)!;
        sessionType.GetField("Owner", F)!.SetValue(session, Player);
        sessionType.GetField("Combat", F)!.SetValue(session, Combat);
        current.SetValue(null, session); combat.SetValue(null, Combat);
        try
        {
            Check((bool)Native(savior, "TryTakeAllEnemyStun")! && !(bool)Native(savior, "TryTakeAllEnemyStun")!,
                "Savior gets a separate one-use all-enemy stun in a real personal turn");
            Check((bool)Call("PersonalImmediateTurnCoordinator", "IsActiveFor", Player)! &&
                !(bool)Call("PersonalImmediateTurnCoordinator", "IsActiveFor", peer)!,
                "real personal coordinator session recognizes only its owner, while teammate keeps ordinary turn");
            Check((bool)Call("MoreThanOneTruthCombatHelper", "IsExtraTurn", Player)! &&
                !(bool)Call("MoreThanOneTruthCombatHelper", "IsExtraTurn", peer)!,
                "Truth extra-turn policy recognizes Deep Feelings and Shadow Dance personal sessions without marking teammate extra");
            Equal((decimal)Call("MindsEyeCombatHelper", "GetVirtualPercent", Hero)!, 42m,
                "Truth virtual Minds Eye applies in the owner's real personal session");
            Equal((decimal)Call("MindsEyeCombatHelper", "GetVirtualPercent", peer.Creature)!, 0m,
                "a teammate with the same upgrade gets no virtual Minds Eye during somebody else's personal turn");
            var ownerCard = GardenCard<MyLegendBeginsHbrgoSaki>();
            object ownerPlay = Call("MoreThanOneTruthCombatHelper", "BeginPlay", ownerCard, false,
                new ResourceInfo { EnergySpent = 2, EnergyValue = 2, StarsSpent = 0, StarValue = 0 })!;
            try
            {
                Equal((decimal)Call("MoreThanOneTruthCombatHelper", "ModifyHitCount", EyeAttack(ownerCard), 1m)!, 3m,
                    "Truth Spirit Smile adds actual two-energy hit bonus to owner attack in a real personal session");
            }
            finally { Call("MoreThanOneTruthCombatHelper", "ClosePlay", ownerPlay); }
            var peerCard = (MyLegendBeginsHbrgoSaki)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(MyLegendBeginsHbrgoSaki))).MutableClone();
            Set(peerCard, "_owner", peer); var attack = EyeAttack(peerCard); Set(attack, "<Attacker>k__BackingField", peer.Creature);
            object peerPlay = Call("MoreThanOneTruthCombatHelper", "BeginPlay", peerCard, false,
                new ResourceInfo { EnergySpent = 2, EnergyValue = 2, StarsSpent = 0, StarValue = 0 })!;
            try
            {
                Equal((decimal)Call("MoreThanOneTruthCombatHelper", "ModifyHitCount", attack, 1m)!, 1m,
                    "teammate's normal attack receives no Spirit Smile hit bonus during owner's personal session");
            }
            finally { Call("MoreThanOneTruthCombatHelper", "ClosePlay", peerPlay); }
        }
        finally { current.SetValue(null, oldCurrent); combat.SetValue(null, oldCombat); }
        Equal((decimal)Call("MindsEyeCombatHelper", "GetVirtualPercent", Hero)!, 0m,
            "personal-session cleanup immediately disables virtual Minds Eye");
        Check(!(bool)Native(savior, "TryTakeAllEnemyStun")!,
            "Savior cannot reuse the interrupted normal turn's spent all-enemy stun after personal turn ends");
        await Task.CompletedTask;
    }
    public static async Task MindsEyeCases()
    {
        GardenReset();
        await SupportGrant<MindsEyePower>(42m); await SupportGrant<MindsEyePower>(42m);
        await SupportGrant<HbrAttackUpPower>(30m); await SupportGrant<HbrAttackUpPower>(30m);
        Equal(EyePercent(null), 84m, "two real Minds Eye instances add independently");
        Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, null), decimal.Floor(Base(6m) * 2.44m),
            "two Minds Eye and two Attack Up instances share one additive original attack area");
        await SupportGrant<MindsEyePower>(20m);
        Check(Hero.Powers.OfType<MindsEyePower>().Count() == 2 && Hero.Powers.OfType<MindsEyePower>().All(eye => EyePotency(eye) == 42m),
            "weaker third Minds Eye is rejected without merging the two existing uses");
        await SupportGrant<MindsEyePower>(50m);
        Check(Hero.Powers.OfType<MindsEyePower>().Select(EyePotency).Order().SequenceEqual(new[] { 42m, 50m }),
            "stronger third Minds Eye replaces the weaker slot");
        foreach (var eye in Hero.Powers.OfType<MindsEyePower>().ToArray())
        {
            await eye.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero });
            await eye.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, new[] { Enemy });
        }
        Check(Hero.Powers.OfType<MindsEyePower>().All(eye => eye.Amount == 1),
            "Minds Eye Count uses do not decay at either side's turn end");
        await EyeResolveAttack(EyeAttack(), 5);
        Check(!Hero.Powers.OfType<MindsEyePower>().Any(),
            "one complete five-hit attack consumes each active one-use Minds Eye only once");

        GardenReset(); H("Sense", "Talent", 50);
        await SupportGrant<MindsEyePower>(42m);
        Equal(EyePotency(Hero.GetPower<MindsEyePower>()!), 42.84m, "Minds Eye's forty-two basis scales with Wisdom two hundred");

        GardenReset(); var card = GardenCard<MyLegendBeginsHbrgoSaki>();
        await SupportGrant<MindsEyePower>(42m);
        object? parent = Call("CardPlayContextHelper", "Begin", card, false);
        try
        {
            await EyeResolveAttack(EyeAttack(card), 2);
            Equal(EyePercent(card), 42m, "Minds Eye remains available to later commands of the same physical card play");
            Check(Hero.GetPower<MindsEyePower>()?.Amount == 1, "first command defers native amount consumption until card completion");
            var childCard = GardenCard<CrushYouLikeABugHbrgoSaki>();
            object? child = Call("CardPlayContextHelper", "Begin", childCard, true);
            try
            {
                Equal(EyePercent(childCard), 0m, "nested different card cannot borrow parent play's already consumed deferred Minds Eye");
                await EyeResolveAttack(EyeAttack(childCard));
                Check(Hero.GetPower<MindsEyePower>()?.Amount == 1,
                    "nested card attack cannot consume the parent play's pending Minds Eye a second time");
                await (Task)Call("CardPlayContextHelper", "Finish", Task.CompletedTask, child)!; child = null;
            }
            finally { if (child != null) Call("CardPlayContextHelper", "Close", child); }
            Equal(EyePercent(card), 42m, "returning from nested card restores parent's deferred Minds Eye for its later command");
            await EyeResolveAttack(EyeAttack(card), 4);
            Check(Hero.GetPower<MindsEyePower>()?.Amount == 1,
                "second attack command of one card play still preserves the single deferred use");
            await (Task)Call("CardPlayContextHelper", "Finish", Task.CompletedTask, parent)!; parent = null;
            Check(Hero.GetPower<MindsEyePower>() == null, "whole card play completion decrements the used real Minds Eye once");
        }
        finally { if (parent != null) Call("CardPlayContextHelper", "Close", parent); }

        GardenReset(); var truth = GardenCard<MoreThanOneTruthHbrgoSaki>(3); GardenPermanent(truth);
        var oldExtra = CombatManager.Instance.PlayersTakingExtraTurn.ToList();
#if HBR_BETA_NATIVE
        object turn = typeof(CombatManager).GetField("_turnState", F)!.GetValue(CombatManager.Instance)!;
        Set(turn, "<PlayersTakingExtraTurn>k__BackingField", new List<MegaCrit.Sts2.Core.Entities.Players.Player> { Player });
#else
        Set(CombatManager.Instance, "_playersTakingExtraTurn", new List<MegaCrit.Sts2.Core.Entities.Players.Player> { Player });
#endif
        try
        {
            Equal((decimal)Call("MindsEyeCombatHelper", "GetVirtualPercent", Hero)!, 42m,
                "Truth grants one Wisdom-scaled virtual baseline Minds Eye in owner's extra turn");
            Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, null), decimal.Floor(Base(6m) * 1.42m),
                "virtual Minds Eye enters the same actual damage area without requiring a real instance");
            await EyeResolveAttack(EyeAttack(), 3);
            Check(!Hero.Powers.OfType<MindsEyePower>().Any(), "virtual Minds Eye never creates or consumes an ordinary cap slot");
            Equal((decimal)Call("MindsEyeCombatHelper", "GetVirtualPercent", Hero)!, 42m,
                "virtual Minds Eye remains after one attack");
            await SupportGrant<MindsEyePower>(42m); await SupportGrant<MindsEyePower>(42m);
            Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, null), decimal.Floor(Base(6m) * 2.26m),
                "two real Minds Eye plus one uncapped virtual Minds Eye all add to attack area");
            Check(Hero.Powers.OfType<MindsEyePower>().Count() == 2,
                "virtual source leaves both ordinary Minds Eye cap slots available");
        }
        finally
        {
#if HBR_BETA_NATIVE
            Set(turn, "<PlayersTakingExtraTurn>k__BackingField", oldExtra);
#else
            Set(CombatManager.Instance, "_playersTakingExtraTurn", oldExtra);
#endif
        }
        Equal((decimal)Call("MindsEyeCombatHelper", "GetVirtualPercent", Hero)!, 0m,
            "Truth's virtual Minds Eye disappears immediately outside an extra turn");

        await MindsEyePersonalMembershipCases();
        GardenReset(); card = GardenCard<MyLegendBeginsHbrgoSaki>();
        var pipeline = RollPipeline(card); LandRoll(pipeline, card, 100);
        decimal before = (decimal)typeof(HBRGOSakiMod.Core.Models.Capabilities.RollGlossaryCapability)
            .GetField("_currentValueMultiplier", F)!.GetValue(pipeline)!;
        await SupportGrant<MindsEyePower>(42m); LandRoll(pipeline, card, 100);
        Equal((decimal)typeof(HBRGOSakiMod.Core.Models.Capabilities.RollGlossaryCapability)
            .GetField("_currentValueMultiplier", F)!.GetValue(pipeline)!, before,
            "the actual card roll-value preparation no longer doubles dice effect for Minds Eye");
    }
}
