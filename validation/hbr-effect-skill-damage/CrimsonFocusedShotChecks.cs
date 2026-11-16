using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

// Real Crimson card/power lifecycle, attribute reads, complete damage/block
// hooks and Focused Shot's received-block hook. Draw and pile-removal visuals
// are isolated; power application/removal and duration arithmetic remain native.
static partial class EffectChecks
{
    static readonly List<decimal> CrimsonDraws = [];
    static bool CrimsonDrawLeaf(decimal count, ref Task<IEnumerable<CardModel>> __result)
    {
        CrimsonDraws.Add(count);
        __result = Task.FromResult<IEnumerable<CardModel>>(Array.Empty<CardModel>());
        return false;
    }

    static void CrimsonReset(bool hbr)
    {
        Reset(hbr); As31O(true); Fate(FateJson(bosses: 0)); CrimsonDraws.Clear();
        foreach ((string name, PileType type) in new[] { ("Hand", PileType.Hand), ("PlayPile", PileType.Play),
                     ("DrawPile", PileType.Draw), ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust) })
            Set(Player.PlayerCombatState, "<" + name + ">k__BackingField", new CardPile(type));
        Set(Player.PlayerCombatState, "_piles", null);
        Set(Combat, "_allCards", new List<CardModel>());
    }

    static async Task CrimsonFocusedShotCases()
    {
        var harmony = new Harmony(ProductionId + ".crimson-focused-shot-leaves");
        var patched = new List<MethodBase>();
        void Patch(MethodBase target, string prefix)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
            patched.Add(target);
        }
        bool wasInProgress = CombatManager.Instance.IsInProgress;
        try
        {
            CombatInProgressWithTurn();
            foreach (Type type in new[] { typeof(CrimsonBladeFlowPower), typeof(NormalStatePower), typeof(ReptureDualSwordsPower),
                         typeof(ReptureBowPower), typeof(FocusedShotPower), typeof(StrengthPower) })
            {
                if (!ModelDb.Contains(type)) ModelDb.Inject(type);
                Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method =>
                         method.Name == "CustomScaledWait" && method.ReturnType == typeof(Task)))
                Patch(wait, nameof(VisualTask));
            foreach (MethodInfo remove in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "RemoveFromCombat"))
                Patch(remove, nameof(CrackRemove));
            Patch(typeof(CardPileCmd).GetMethods(F).Single(method => method.Name == "Draw" &&
                method.GetParameters().Length == 4 && method.GetParameters()[1].ParameterType == typeof(decimal)), nameof(CrimsonDrawLeaf));

            foreach (bool hbr in new[] { true, false })
            foreach (bool upgraded in new[] { false, true })
            {
                CrimsonReset(hbr);
                decimal incomingBefore = Actual(Hero, Enemy, 6m, ValueProp.Move, null);
                Power<NormalStatePower>(Hero, 1);
                ChihayaClassmateHbrgoSaki selection = ClarityCard<ChihayaClassmateHbrgoSaki>();
                Player.PlayerCombatState.DrawPile.AddInternal(selection, -1, true);
                var card = ClarityCard<CrimsonBladeFlowHbrgoSaki>();
                Check(card.EnergyCost.GetWithModifiers(CostModifiers.All) == 2 && !Hero.HasPower<MagicSwordModePower>(),
                    "Crimson remains a two-energy power available without magic sword");
                await UnifiedPlayCard(card, upgraded);
                CrimsonBladeFlowPower crimson = Hero.GetPower<CrimsonBladeFlowPower>()!;
                Check(crimson != null && crimson.Amount == (upgraded ? 3 : 2) &&
                    !Hero.HasPower<NormalStatePower>() && selection.HasBeenRemovedFromState,
                    "Crimson's real card grants two/three turns, removes Normal State and the mode-selection card");
                Equal(crimson.ModifyDamageMultiplicative(Enemy, 6m, ValueProp.Move, Hero, card), hbr ? 1m : 2m,
                    "Crimson adds no old outgoing damage multiplier in HBR and retains legacy doubling");
                Equal(crimson.ModifyDamageMultiplicative(Hero, 6m, ValueProp.Move, Enemy, null!), hbr ? 1m : 2m,
                    "Crimson adds no old incoming damage multiplier in HBR and retains legacy doubling");
                if (hbr)
                {
                    Check(HeroStat("Strength") == 200m && HeroStat("Dexterity") == 200m &&
                        HeroStat("Intelligence") == 100m && HeroStat("Vitality") == 0m &&
                        HeroStat("Spirit") == 0m && HeroStat("Luck") == 100m,
                        "HBR Crimson doubles only final Strength/Dexterity and zeros final Vitality/Spirit");
                    Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, null), decimal.Floor(Base(6m, 200m)),
                        "HBR Crimson damage follows the doubled attribute curve without a second times-two factor");
                    Check(Actual(Hero, Enemy, 6m, ValueProp.Move, null) > incomingBefore,
                        "HBR Crimson's zero defensive attributes increase incoming damage");
                }
                else
                {
                    Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, null), 12m,
                        "legacy Crimson's full outgoing pipeline retains raw six doubled to twelve");
                    Equal(Actual(Hero, Enemy, 6m, ValueProp.Move, null), incomingBefore * 2m,
                        "legacy Crimson's full incoming pipeline retains times-two damage");
                }
                Equal(Preview(Enemy, Hero, 6m, ValueProp.Move, null), Actual(Enemy, Hero, 6m, ValueProp.Move, null),
                    "Crimson's outgoing preview and actual damage boundary agree");
                Equal(Preview(Hero, Enemy, 6m, ValueProp.Move, null), Actual(Hero, Enemy, 6m, ValueProp.Move, null),
                    "Crimson's incoming preview and actual damage boundary agree");
                Equal(HeroBlock(), hbr ? 1000m : 10m, "Crimson keeps its block doubling in both numeric modes");
                await crimson.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), Hero.Side, new[] { Hero }, Combat);
                Check(CrimsonDraws.SequenceEqual(new[] { 1m }), "Crimson keeps one extra draw at each ordinary turn start");

                var dual = Power<ReptureDualSwordsPower>(Hero, 1);
                Equal(dual.ModifyBlockMultiplicative(Hero, 5m, ValueProp.Move, null!, null!), 1m,
                    "dual swords has no retired additional block multiplier");
                Check((bool)Call("ModeTransitionHelper", "IsWeaponModeWeakened", Hero)!,
                    "Crimson continues to suppress bow deck scaling; dual-sword energy is retired");
                Powers(Hero).Remove(dual);
                var dream = Power<NoSelfInDreamPower>(Hero, 1);
                var result = Blank<DamageResult>(); Set(result, "<Receiver>k__BackingField", Enemy);
                Set(result, "<UnblockedDamage>k__BackingField", 100);
                Hits.Clear();
                await dream.AfterDamageGiven(new ThrowingPlayerChoiceContext(), Hero, result, ValueProp.Move, Enemy, null!);
                Check(Hits.Count == 0, "No Self In Dream keeps its Crimson exemption from allied damage");
                Powers(Hero).Remove(dream);

                int turns = crimson.Amount;
                await UnifiedPlayCard(ClarityCard<DoublePowersHbrgoSaki>(), false);
                Check(crimson.Amount == turns * 2, "Double Powers continues to double Crimson's duration");
                var protection = Blank<DeflectorProtection>();
                Set(protection, "<IsMutable>k__BackingField", true); Set(protection, "_owner", Player);
                Set(protection, "<Id>k__BackingField", ModelDb.GetId(typeof(DeflectorProtection)));
                Set(protection, "_hasInitializedDp", true); Set(protection, "_dp", 1000);
                Set(Player, "_relics", new List<RelicModel> { FateManual, protection });
                await crimson.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), Hero.Side, new[] { Hero });
                Check(crimson.Amount == turns * 2 - 1 && protection.Dp == 900,
                    "Crimson still loses ten percent of current DP and one duration at ordinary turn end");
                await PowerCmd.Remove(crimson);
                Check(!Hero.HasPower<CrimsonBladeFlowPower>() && Hero.HasPower<ReptureDualSwordsPower>(),
                    "Crimson's real removal re-enters dual swords");
                if (hbr)
                    Check(HeroStat("Strength") == 100m && HeroStat("Dexterity") == 100m &&
                        HeroStat("Vitality") == 100m && HeroStat("Spirit") == 100m,
                        "removing Crimson immediately restores the unmodified live attributes");
            }

            foreach (bool hbr in new[] { true, false })
            {
                CrimsonReset(hbr);
                await UnifiedPlayCard(ClarityCard<FocusedShotHbrgoSaki>(), false);
                FocusedShotPower focused = Hero.GetPower<FocusedShotPower>()!;
                await focused.AfterBlockGained(Hero, 375m, ValueProp.Move, null!);
                Check(!Hero.HasPower<StrengthPower>(), "Focused Shot still requires bow form before generating Strength");
                Power<ReptureBowPower>(Hero, 1);
                Equal(focused.ModifyBlockMultiplicative(Hero, 500m, ValueProp.Move, null!, null!), .75m,
                    "Focused Shot retains its bow block reduction to seventy-five percent");
                foreach (decimal gained in new[] { 11m, 12m, 47m, 48m, 375m, 384m })
                {
                    Powers(Hero).RemoveAll(power => power is StrengthPower);
                    await focused.AfterBlockGained(Hero, gained, ValueProp.Move, null!);
                    Equal(Hero.GetPowerAmount<StrengthPower>(), decimal.Floor(gained / (hbr ? 48m : 12m)),
                        "Focused Shot's actual received-block hook grants floored Strength from " + gained + " (HBR " + hbr + ")");
                }
                Check(focused.Description.LocEntryKey == (hbr ? "FOCUSED_SHOT_POWER.hbrDescription" : "FOCUSED_SHOT_POWER.description"),
                    "Focused Shot selects only the current numeric mode's description");
                var crimson = Power<CrimsonBladeFlowPower>(Hero, 2);
                Check(crimson.Description.LocEntryKey == (hbr ? "CRIMSON_BLADE_FLOW_POWER.hbrDescription" : "CRIMSON_BLADE_FLOW_POWER.description"),
                    "Crimson selects only the current numeric mode's description");
            }
        }
        finally
        {
            foreach (MethodBase target in patched) harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
            CombatInProgress(wasInProgress); Reset(); As31O(false);
        }
    }
}
