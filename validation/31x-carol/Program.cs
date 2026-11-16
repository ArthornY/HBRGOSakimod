using System.Reflection;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static class CarolStandalone { public static void Run(string path) => EffectChecks.RunCarol89(path); }
static partial class EffectChecks
{
    static ReadySteadyDieHbrgoSaki C89(params int[] ids) => XGarden<ReadySteadyDieHbrgoSaki>(ids);
    static async Task CarolCases89()
    {
        XCardReset(); var card = C89();
        Check(card is IExtraServingCard && card is IThirtyOneXMemberCard m && m.ThirtyOneXMember == ThirtyOneXMember.CarolReaper, "89 actual card opts into generic ExtraServing and Carol31X");
        Check(card.Type == CardType.Attack && card.Rarity == CardRarity.Rare && card.TargetType == TargetType.AnyEnemy && card.MaxUpgradeLevel == 7, "89 two-energy rare single-target attack has seven upgrades");
        Equal(card.EnergyCost.Canonical, 2, "89 native canonical cost2"); Equal(card.DynamicVars.Damage.BaseValue, 5.94m, "89 exact99percent stored as5point94 native units");
        Check(!card.Keywords.Contains(CardKeyword.Exhaust), "89 base is not Exhaust");
        await ChasePlay(card);
        Check(XCardAttacks.Single() is { Raw: 5.94m, Hits: 4 } && XCardAttacks.Single().Targets.SequenceEqual(new[] { Enemy }), "89 native wrapper requests99percent four times against selected target");
        Check(XCardHits.Count == 4 && XCardHits.All(hit => hit.Resource == 1.3m && hit.Destruction == 2.375m / 3m), "89 actual canonical hits have HP30percent and normalized total destruction2point375");
        Equal(Player.PlayerCombatState!.Energy, 18, "89 native manual play spends2 once"); Check(card.Pile == Player.PlayerCombatState.DiscardPile, "89 native wrapper discards the reusable attack");
        XCardReset(); card = C89(0); Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All), 1, "89 Flash cost2to1");
        foreach (int energy in new[] { 0, 1, 2, 5 })
        {
            XCardReset(); card = C89(); Set(Player.PlayerCombatState!, "_energy", energy); Check(card.CanPlay(), "89 native CanPlay permits ExtraServing shortage " + energy);
            var paid = await card.SpendResources(); Equal(paid.Item1, Math.Min(energy, 2), "89 native resources report actual payment " + energy);
            Equal(Hero.GetPowerAmount<HbrEnergyDebtPower>(), Math.Max(0, 2 - energy), "89 debt contains only real unpaid cost " + energy);
        }
        XCardReset(); card = C89(0); Set(Player.PlayerCombatState!, "_energy", 0); await card.SpendResources(); Equal(Hero.GetPowerAmount<HbrEnergyDebtPower>(), 1, "89 Flash debt uses final1cost");
        XCardReset(); card = C89(1); await ChasePlay(card);
        Equal(XCardAttacks.Single().Attack, 0, "89 ThunderRoar grants no attack buff before its own damage"); Equal(XPotency<HbrAttackUpPower>(), 40, "89 ThunderRoar leaves actual40percent attack buff afterward"); Equal(Hero.GetPowerAmount<HbrAttackUpPower>(), 1, "89 postattack buff retains one counted future attack");
        XCardReset(); card = C89(2); Creature second = XExtraEnemy(); await ChasePlay(card);
        Check(card.TargetType == TargetType.AllEnemies && XCardAttacks.Single().Targets.ToHashSet().SetEquals(new[] { Enemy, second }), "89 LightningStorm native targeting includes both enemies"); Equal(XCardHits.Count, 8, "89 one four-hit command hits each of two enemies");
        XCardReset(); card = C89(3); await ChasePlay(card); Check(XCardAttacks.Single().Eye, "89 LegendaryMode grants real MindsEye before attack"); Check(!Hero.HasPower<MindsEyePower>(), "89 actual whole-card completion consumes that MindsEye once");
        XCardReset(); card = C89(4); await ChasePlay(card); Check(XCardHits.All(hit => hit.Resource == 1.5m), "89 Justice upgrades only HP conditional multiplier to1point5");
        XCardReset(); card = C89(4); await PowerCmd.Apply<HighDimensionalShellPower>(new ThrowingPlayerChoiceContext(), Enemy, 10000000m, Hero, null); await ChasePlay(card); Check(XCardHits.All(hit => hit.Resource == 1m), "89 Justice never grants HP bonus against activeDP");
        XCardReset(); card = C89(5); await ChasePlay(card); Equal(XCardAttacks.Single().CritDamage, 0, "89 bareHP without actualBreak does not satisfy GoodChance");
        XCardReset(); card = C89(5); Power<BreakPower>(Enemy, 1); await ChasePlay(card); Equal(XCardAttacks.Single().CritDamage, 25, "89 realBreak grants25percent critical damage before attack");
        Check(Hero.HasPower<HbrCriticalDamageUpPower>(), "89 GoodChance timed buff is not consumed by that attack");
        await Hero.GetPower<HbrCriticalDamageUpPower>()!.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero }); Check(!Hero.HasPower<HbrCriticalDamageUpPower>(), "89 GoodChance ends on owner turn end");
        XCardReset(); card = C89(6); NewDp(0); await ChasePlay(card); Check(XCardHits.All(hit => hit.Amp == 0), "89 mask absent at zeroDP");
        XCardReset(); card = C89(6); NewDp(1); XCardNested = XDrainDp = true; await ChasePlay(card);
        Check(XCardHits.Where(hit => hit.Depth == 0).All(hit => hit.Amp == 30m), "89 mask is30ordinarypercent on each primary hit");
        Check(XCardHits.Where(hit => hit.Depth != 0).All(hit => hit.Amp == 0m && hit.Resource == 1m && hit.Destruction == 1m), "89 same-source nested canonical damage inherits no mask/HP/destruction modifiers");
        ExecutionContext.Run(XCardCaptured!, _ => Equal((decimal)Call("CarolHeroCombatHelper", "GetDamageAmplificationPercent", card, Hero, Enemy, ValueProp.Move)!, 0, "89 closed captured receipt falls back to live preview instead of a stale snapshot"), null);
        Equal((decimal)Call("CarolHeroCombatHelper", "GetDamageAmplificationPercent", card, Hero, Enemy, ValueProp.Unpowered)!, 0, "89 unpowered preview cannot borrow mask");
        XCardReset(); card = C89(6); Set(Player, "<Character>k__BackingField", Blank<HbrgoSaki30A>());
        var reign = (MomentOfReign)ModelDb.Relic<MomentOfReign>().ToMutable(); Set(reign, "_owner", Player); Set(Player, "_relics", Player.Relics.Append(reign).ToList());
        Creature phantom = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!; Power<PhantomPower>(phantom, 1); Set(Combat, "_allies", new List<Creature> { Hero, phantom });
        Power<PhantomHighDimensionalShellPower>(phantom, 20); await ChasePlay(card); Check(XCardHits.All(hit => hit.Amp == 30m), "89 mask checks actual30A primary phantomDP rather than nonexistent heroDP");
        XCardReset(); card = C89(0, 1, 2, 3, 4, 5, 6); XSaved(card, "89"); await ChasePlay(card);
        Check(XCardAttacks.Single().Eye && !card.Keywords.Contains(CardKeyword.Exhaust) && card.Pile == Player.PlayerCombatState!.DiscardPile, "89 all seven upgrades keep normal reusable attack lifecycle");
        Check(((IReadOnlyList<int>)Native(card, "GetAvailableUpgradeEffects")!).Count == 0, "89 all seven choices obtained once without eighth choice");
        XCardReset(); card = C89(0); Hero.GetPower<XPlayObserver89>()!.Replays = 2; Set(Player.PlayerCombatState!, "_energy", 0); await ChasePlay(card);
        Check(XCardAttacks.Count == 3 && Hero.GetPower<XPlayObserver89>()!.Paid.SequenceEqual(new[] { 0 }), "89 actual native replay series executes three times but pays once"); Equal(Hero.GetPowerAmount<HbrEnergyDebtPower>(), 1, "89 replay series creates debt only once");
        XCardReset(); card = C89(6); NewDp(1); Hero.GetPower<XPlayObserver89>()!.Replays = 1; XDrainDp = true; await ChasePlay(card);
        Check(XCardHits.Take(4).All(hit => hit.Amp == 30m) && XCardHits.Skip(4).All(hit => hit.Amp == 0m), "89 each native replay snapshots DP anew rather than inheriting the first execution's mask");
        XCardReset(); card = C89(); Set(Player.PlayerCombatState!, "_energy", 0); await ChasePlay(card, true); Equal(Hero.GetPowerAmount<HbrEnergyDebtPower>(), 0, "89 genuine autoplay creates no unpaid energy debt");
        XCardReset(); card = C89(4, 6); NewDp(1); await XActualCannon(card);
        Check(XCardHits.Single() is { Amp: 0m, Resource: 1m, Destruction: 1m }, "89 real CannonStats command retaining card source inherits none of its unique modifiers");
        XCardReset(); card = C89(6); NewDp(1); XCardThrow = true;
        try { await ChasePlay(card); throw new Exception("missing controlled error"); } catch (InvalidOperationException error) { Check(error.Message.Contains("controlled"), "89 command fault remains observable"); }
        Check(AsyncValue("CarolHeroCombatHelper", "Current") == null, "89 failure closes active card receipt");
        XCardReset(false); card = C89(6); NewDp(1); Equal((decimal)Call("CarolHeroCombatHelper", "GetDamageAmplificationPercent", card, Hero, Enemy, ValueProp.Move)!, 0, "89 legacy mode has no HBR conditional bonus");
    }
    public static void RunCarol89(string path) => XRun(path, "89", () => CarolCases89().GetAwaiter().GetResult());
}
