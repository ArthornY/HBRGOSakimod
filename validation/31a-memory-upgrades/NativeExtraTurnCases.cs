using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;

static partial class EffectChecks
{
    static async Task GardenNativeExtraTurnCases()
    {
        var h = new Harmony(ProductionId + ".truth-native-entry");
        h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.MoreThanOneTruthNativeExtraTurnPatch", true)!).Patch();
        try
        {
            GardenReset(); var card = GardenCard<MoreThanOneTruthHbrgoSaki>(2, 5); GardenPermanent(card);
            Set(Player.PlayerCombatState!, "_energy", 0);
            await Hook.AfterPlayerTurnStart(Combat, new ThrowingPlayerChoiceContext(), Player);
            Equal(Player.PlayerCombatState!.Energy, 0m, "Truth native after-turn-start patch ignores an ordinary turn");
            Check(!Hero.Powers.OfType<HbrComboSmallPower>().Any(), "ordinary turn start does not grant Nightmare Small combo");
            var previous = CombatManager.Instance.PlayersTakingExtraTurn.ToList();
#if HBR_BETA_NATIVE
            object turn = typeof(CombatManager).GetField("_turnState", F)!.GetValue(CombatManager.Instance)!;
            Set(turn, "<PlayersTakingExtraTurn>k__BackingField", new List<Player> { Player });
#else
            Set(CombatManager.Instance, "_playersTakingExtraTurn", new List<Player> { Player });
#endif
            try
            {
                await Hook.AfterPlayerTurnStart(Combat, new ThrowingPlayerChoiceContext(), Player);
                Equal(Player.PlayerCombatState!.Energy, 1m, "real native extra-turn start hook grants exactly one 1MORE energy");
                Check(Hero.Powers.OfType<HbrComboSmallPower>().Single() is { Amount: 5 },
                    "real native extra-turn start hook grants one five-use Nightmare Small combo");
            }
            finally
            {
#if HBR_BETA_NATIVE
                Set(turn, "<PlayersTakingExtraTurn>k__BackingField", previous);
#else
                Set(CombatManager.Instance, "_playersTakingExtraTurn", previous);
#endif
            }
            await Hook.AfterPlayerTurnStart(Combat, new ThrowingPlayerChoiceContext(), Player);
            Equal(Player.PlayerCombatState!.Energy, 1m, "ordinary turn after native extra entry does not repeat 1MORE");
            Check(Hero.Powers.OfType<HbrComboSmallPower>().Count() == 1,
                "ordinary turn after native extra entry does not add another Nightmare instance");
        }
        finally { h.UnpatchAll(h.Id); }
    }
}
