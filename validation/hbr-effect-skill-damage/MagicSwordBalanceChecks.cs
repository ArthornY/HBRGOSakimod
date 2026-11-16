using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

// Exercise production damage hooks and destruction scopes. Only the energy command's
// presentation/lifecycle leaf is substituted; this is not an in-game turn test.
static partial class EffectChecks
{
    static readonly List<(Player Player, decimal Amount)> MagicSwordEnergyCalls = [];

    static bool MagicSwordGainEnergyLeaf(object[] __args, ref Task __result)
    {
        MagicSwordEnergyCalls.Add((__args.OfType<Player>().Single(),
            Convert.ToDecimal(__args.First(value => value is int or decimal))));
        __result = Task.CompletedTask;
        return false;
    }

    static void MagicSwordDeck(int count)
    {
        Set(Player, "<Deck>k__BackingField", new CardPile(PileType.Deck));
        for (int index = 0; index < count; index++)
            Player.Deck.AddInternal(Owned<StrikeIronclad>(), -1, true);
    }

    static decimal MagicSwordFactor(Creature? dealer) =>
        (decimal)Invoke(typeof(MagicSwordModePower).GetMethod("GetDeckBonusMultiplier", F)!, null, dealer)!;

    static async Task MagicSwordBalanceCases()
    {
        CardPile oldDeck = Player.Deck;
        CardPile oldHand = Player.PlayerCombatState.Hand;
        CardPile oldExhaust = Player.PlayerCombatState.ExhaustPile;
        try
        {
            foreach (bool hbr in new[] { true, false })
            foreach (int count in new[] { 0, 10, 25 })
            {
                Reset(hbr); As31O(true); MagicSwordDeck(count);
                var card = Owned<StrikeIronclad>();
                decimal baseline = Actual(Enemy, Hero, 6m, ValueProp.Move, card);
                var magic = Power<MagicSwordModePower>(Hero, 1);
                decimal factor = 1m + count * .02m;
                Equal(MagicSwordFactor(Hero), factor, $"magic sword reads {count} permanent cards (HBR {hbr})");
                Equal(MagicSwordFactor(Enemy), 1m, "an unrelated dealer receives no magic sword deck factor");
                Equal(MagicSwordFactor(null), 1m, "a null dealer receives no magic sword deck factor");

                // The fixture executes the numeric modifier boundary. HBR floors
                // there; the legacy native HP command floors only afterwards.
                decimal expected = hbr ? decimal.Floor(Base(6m) * factor) : 6m * factor;
                decimal preview = Preview(Enemy, Hero, 6m, ValueProp.Move, card);
                decimal actual = Actual(Enemy, Hero, 6m, ValueProp.Move, card);
                Equal(actual, expected, $"magic sword real damage includes one {factor} deck factor (HBR {hbr})");
                Equal(preview, actual, "magic sword damage preview agrees with the real damage pipeline");
                Equal(Actual(Enemy, Hero, 6m, ValueProp.Unpowered, card), 6m,
                    "literal Unpowered damage receives no magic sword multiplier");

                Set(Player.PlayerCombatState, "<Hand>k__BackingField", new CardPile(PileType.Hand));
                Set(Player.PlayerCombatState, "<ExhaustPile>k__BackingField", new CardPile(PileType.Exhaust));
                Player.PlayerCombatState.Hand.AddInternal(Owned<DefendIronclad>(), -1, true);
                Player.PlayerCombatState.ExhaustPile.AddInternal(Owned<StrikeIronclad>(), -1, true);
                Equal(MagicSwordFactor(Hero), factor, "generated hand and exhausted combat cards do not increase permanent deck size");
                Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, card), actual,
                    "combat-pile changes do not change magic sword's permanent deck damage bonus");

                MethodInfo damageHook = typeof(MagicSwordModePower).GetMethods(F | BindingFlags.DeclaredOnly).Single(method =>
                    method.Name == "ModifyDamageMultiplicative" && method.GetParameters().Length == 5);
                Equal((decimal)Invoke(damageHook, magic, Hero, 6m, ValueProp.Move, Hero, card)!, 1m,
                    "magic sword damage bonus excludes friendly targets");
                Equal((decimal)Invoke(damageHook, magic, Enemy, 0m, ValueProp.Move, Hero, card)!, 1m,
                    "magic sword damage bonus does not turn zero damage into damage");
                Equal((decimal)Invoke(damageHook, magic, Hero, 6m, ValueProp.Move, Enemy, card)!, 1m,
                    "an enemy attack does not inherit the defending hero's magic sword bonus");

                var destruction = Power<DestructionRatePower>(Enemy, 1);
                Equal(magic.ModifyPowerAmountGivenMultiplicative(destruction, Hero, 10m, Enemy, card), factor,
                    "explicit destruction application receives exactly one magic sword deck multiplier");
                Equal(magic.ModifyPowerAmountGivenMultiplicative(destruction, Enemy, 10m, Enemy, card), 1m,
                    "another giver cannot use the hero's magic sword destruction multiplier");
                Equal(magic.ModifyPowerAmountGivenMultiplicative(destruction, Hero, -10m, Enemy, card), 1m,
                    "removing destruction does not receive the positive gain bonus");
                Equal(magic.ModifyPowerAmountGivenMultiplicative(Power<StrengthPower>(Enemy, 1), Hero, 10m, Enemy, card), 1m,
                    "magic sword deck size does not amplify unrelated powers");
                Powers(Hero).Remove(magic);
                Equal(MagicSwordFactor(Hero), 1m, "removing magic sword removes its deck factor immediately");
                Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, card), baseline,
                    "after magic sword ends, real damage returns to its prior value");
            }

            Reset(); As31O(true); MagicSwordDeck(25);
            var source = Owned<StrikeIronclad>();
            Power<MagicSwordModePower>(Hero, 1);
            var rate = Power<DestructionRatePower>(Enemy, DestructionRatePower.BaselineAmount);
            decimal gain = 0m;
            using ((IDisposable)Call("HbrDestructionGainHelper", "BeginSkill", Hero, source, 4m, 3m)!)
            {
                for (int segment = 0; segment < 4; segment++)
                {
                    using ((IDisposable)Call("HbrDestructionGainHelper", "BeginHit", Hero, source, ValueProp.Move)!)
                    {
                        Call("HbrDestructionGainHelper", "ObserveHit", Enemy);
                        decimal share = (decimal)Call("HbrDestructionGainHelper", "GetGain", Enemy, Hero, source, ValueProp.Move)!;
                        gain += share;
                        Equal(share, 2.25m, "four-hit magic sword skill shares its 9-point destruction budget evenly");
                        Equal((decimal)Call("HbrDestructionGainHelper", "GetGain", Enemy, Hero, source, ValueProp.Move)!, 0m,
                            "a repeated receive path cannot claim the same destruction share twice");
                    }
                }
            }
            Equal(gain, 9m, "25 permanent cards raise a six-point HBR destruction budget to nine, not thirteen-and-a-half");

            using ((IDisposable)Call("HbrDestructionGainHelper", "BeginSkill", Hero, source, 4m, 3m)!)
            {
                for (int segment = 0; segment < 4; segment++)
                {
                    using ((IDisposable)Call("HbrDestructionGainHelper", "BeginHit", Hero, source, ValueProp.Move)!)
                        Invoke(typeof(DestructionRatePower).GetMethod("AddHbrHitGain", F)!, rate, Hero, source, ValueProp.Move);
                }
            }
            Equal(rate.DisplayAmount, 9m, "real HBR destruction receiver keeps fractional shares and applies magic sword only once");
            Equal((decimal)Call("HbrDestructionGainHelper", "GetGain", Enemy, Hero, source, ValueProp.Unpowered)!, 0m,
                "unpowered damage receives no automatic HBR destruction budget");

            await MagicSwordEnergyCases();
        }
        finally
        {
            Set(Player, "<Deck>k__BackingField", oldDeck);
            Set(Player.PlayerCombatState, "<Hand>k__BackingField", oldHand);
            Set(Player.PlayerCombatState, "<ExhaustPile>k__BackingField", oldExhaust);
            As31O(false); Reset();
        }
    }

    static async Task MagicSwordEnergyCases()
    {
        Reset(); As31O(true);
        var magic = Power<MagicSwordModePower>(Hero, 1);
        var harmony = new Harmony(ProductionId + ".magic-sword-energy-leaf");
        MethodInfo prefix = typeof(EffectChecks).GetMethod(nameof(MagicSwordGainEnergyLeaf), F)!;
        MethodInfo[] gains = typeof(PlayerCmd).GetMethods(F)
            .Where(method => method.Name == "GainEnergy" && method.ReturnType == typeof(Task)).ToArray();
        Check(gains.Length > 0, "native energy command is available for the magic sword reset hook");
        MagicSwordEnergyCalls.Clear();
        try
        {
            foreach (MethodInfo method in gains) harmony.Patch(method, prefix: new HarmonyMethod(prefix));
            await magic.AfterEnergyReset(Player);
            Check(MagicSwordEnergyCalls.Count == 1 && MagicSwordEnergyCalls[0] == (Player, 1m),
                "real magic sword energy-reset hook requests one energy for its holder");
            await magic.AfterEnergyReset(Player);
            Check(MagicSwordEnergyCalls.Count == 2 && MagicSwordEnergyCalls.All(call => call == (Player, 1m)),
                "each subsequent energy reset requests one energy again");
            await magic.AfterEnergyReset(Blank<Player>());
            Check(MagicSwordEnergyCalls.Count == 2, "another player's reset does not trigger this holder's magic sword energy");
        }
        finally
        {
            foreach (MethodInfo method in gains) harmony.Unpatch(method, prefix);
            MagicSwordEnergyCalls.Clear();
        }
    }
}
