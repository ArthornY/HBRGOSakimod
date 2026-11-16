using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

// Cross-checks the stat implementation independently of the broad stat suite:
// real rune predicate consumers, local cannon state, temporary native powers
// and the actual native block -> AfterBlockGained -> Strength chain.
static partial class EffectChecks
{
    static bool InteractionRune;
    static bool InteractionRuneLeaf(Creature creature, ref bool __result)
    { __result = InteractionRune && ReferenceEquals(creature, Hero); return false; }
    static bool InteractionNotEnding(ref bool __result) { __result = false; return false; }
    static object InteractionStats(Creature creature) => H("Stats", creature)!;
    static decimal InteractionStat(Creature creature, string name) => (decimal)H("Stat", InteractionStats(creature), name)!;
    static decimal InteractionConvert(Creature target, Creature dealer) => (decimal)Call("HbrDamageSkillAdapter", "ConvertBaseDamage", target, dealer, 6m, ValueProp.Move, null)!;
    static decimal InteractionCannon(Creature cannon, Func<decimal> read) => (decimal)H("Cannon", cannon, read)!;
    static void InteractionReset(bool hbr = true)
    {
        Reset(hbr); As31O(true); CombatInProgressWithTurn(); InteractionRune = false;
        Set(Hero, "_block", 0); Set(Enemy, "_block", 0);
        var state = Player.PlayerCombatState!; Set(state, "_player", Player); Set(state, "_pets", new List<Creature>());
        Set(state, "_piles", null);
        foreach (var row in new[] { ("Hand", PileType.Hand), ("DrawPile", PileType.Draw), ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust), ("PlayPile", PileType.Play) })
            Set(state, "<" + row.Item1 + ">k__BackingField", new CardPile(row.Item2));
        Set(Player, "<Deck>k__BackingField", new CardPile(PileType.Deck)); Set(Player, "_runPiles", null);
        Set(Combat, "_allCards", new List<CardModel>());
    }
    static T InteractionOwned<T>() where T : AbstractModel, new()
    {
        T value = ModelDb.Contains(typeof(T)) ? (T)ModelDb.GetById<AbstractModel>(ModelDb.GetId(typeof(T))).MutableClone() : Owned<T>();
        Set(value, "_owner", Player); return value;
    }
    static void InteractionSakura(Creature owner, int copies)
    {
        var power = Power<SakuraRainPower>(owner, 1);
        Set(power, "<IsDamageReductionActive>k__BackingField", true);
        Set(power, "<ActiveCopies>k__BackingField", copies);
    }
    static async Task CrimsonStatInteractionCases()
    {
        foreach (Type type in new[] { typeof(StrengthPower), typeof(DexterityPower), typeof(FlexPotionPower), typeof(SpeedPotionPower), typeof(FlexPotion), typeof(SpeedPotion) })
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        var harmony = new Harmony(ProductionId + ".crimson-stat-interactions");
        var installed = new List<MethodInfo>();
        void Leaf(MethodInfo target, string prefix)
        { harmony.Patch(target, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!)); installed.Add(target); }
        try
        {
            Leaf(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(InteractionNotEnding));
            Leaf(Mod.GetType("HBRGOSakiMod.Core.Integration.HbrgoSakiHextechCompatLoader", true)!.GetMethod("HasDoubleSwordsDoubleRune", F)!, nameof(InteractionRuneLeaf));
            foreach (MethodInfo method in typeof(Cmd).GetMethods(F).Where(m => (m.Name is "Wait" or "CustomScaledWait") && m.ReturnType == typeof(Task))) Leaf(method, nameof(VisualTask));
            foreach (MethodInfo method in typeof(SfxCmd).GetMethods(F).Where(m => m.Name == "Play")) Leaf(method, nameof(Skip));
            IsolateCenterVisuals();
            InteractionVirtualRuneCases();
            InteractionCannonCalamityCases();
            await InteractionTemporaryPowerCases();
            await InteractionNativeFocusedBlockCases();
        }
        finally
        {
            foreach (MethodInfo method in installed.Distinct()) harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id);
            InteractionRune = false; CombatInProgress(false); As31O(false); Reset();
        }
    }
    static void InteractionVirtualRuneCases()
    {
        foreach (bool hbr in new[] { true, false })
        {
            InteractionReset(hbr); var dual = Power<ReptureDualSwordsPower>(Hero, 1);
            var strike = InteractionOwned<StrikeIronclad>();
            decimal outgoing = Actual(Enemy, Hero, 6m, ValueProp.Move, strike);
            decimal incoming = Actual(Hero, Enemy, 10m, ValueProp.Move, null);
            InteractionRune = true;
            if (hbr)
            {
                Equal(InteractionStat(Hero, "Strength"), 200m, "virtual Crimson rune doubles six-stat Strength without a Crimson power instance");
                Equal(InteractionStat(Hero, "Dexterity"), 200m, "virtual Crimson rune doubles six-stat Dexterity");
                Equal(InteractionStat(Hero, "Vitality"), 0m, "virtual Crimson rune sets defensive Vitality to zero");
                Equal(InteractionStat(Hero, "Spirit"), 0m, "virtual Crimson rune sets defensive Spirit to zero");
                decimal coefficient = (decimal)H("Outgoing", 200m, 200m, 150m, false, 6m)!;
                Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, strike), decimal.Floor(coefficient),
                    "virtual HBR Crimson changes attribute curve once without the retired dual-swords damage penalty");
                Equal(Actual(Hero, Enemy, 10m, ValueProp.Move, null), decimal.Floor((decimal)H("Incoming", 150m, 0m, 0m, false, 10m)!),
                    "virtual HBR Crimson incoming damage uses actual zero-defense curve without another legacy double");
            }
            else
            {
                Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, strike), outgoing + 6m,
                    "virtual rune retains its legacy grouped100percentage-point bonus without the retired dual-swords penalty");
                Equal(Actual(Hero, Enemy, 10m, ValueProp.Move, null), incoming * 2m,
                    "virtual rune retains its legacy incoming damage doubling");
            }
            Equal(Hook.ModifyBlock(Combat, Hero, 5m, ValueProp.Move, strike, null, out _), hbr ? 1000m : 10m,
                "virtual Crimson preserves the existing twofold block gain in " + (hbr ? "HBR" : "legacy"));
            Power<CrimsonBladeFlowPower>(Hero, 2);
            Equal(Hook.ModifyBlock(Combat, Hero, 5m, ValueProp.Move, strike, null, out _), hbr ? 1000m : 10m,
                "real Crimson plus the rune never duplicates the virtual stance's block modifier");
            if (hbr) Equal(InteractionStat(Hero, "Strength"), 200m, "real and virtual Crimson presence never stacks final attribute doubling");
            InteractionRune = false;
        }
    }
    static void InteractionCannonCalamityCases()
    {
        InteractionReset(); H("Sense", "Madness", 25); H("Sense", "LeadershipCharm", 40);
        Power<StrengthPower>(Hero, 20); Power<DexterityPower>(Hero, 10); Power<HbrCalamityPower>(Hero, 10);
        InteractionSakura(Hero, 2);
        decimal maxHp = Hero.MaxHp;
        var protection = InteractionOwned<DeflectorProtection>(); Set(protection, "_hasInitializedDp", true); Set(protection, "_dp", 777);
        var millsage = InteractionOwned<Millsage>();
        Set(Player, "_relics", new List<RelicModel> { State<RelicModel>("Manual"), protection, millsage });
        int maxDp = protection.MaxDp;
        var crimson = Power<CrimsonBladeFlowPower>(Hero, 2);
        Equal(InteractionStat(Hero, "Strength"), 400m, "Crimson applies after Strength40percent and owner Calamity10");
        Equal(InteractionStat(Hero, "Dexterity"), 484m, "Crimson final doubling preserves independent non100 raw Dexterity");
        Equal(InteractionStat(Hero, "Vitality"), 0m, "two Sakura contributions cannot restore Vitality after final Crimson zeroing");
        Equal(InteractionStat(Hero, "Spirit"), 0m, "two Sakura contributions cannot restore Spirit after final Crimson zeroing");
        Check(Hero.MaxHp == maxHp && protection.MaxDp == maxDp && protection.Dp == 777,
            "computed zero defensive stats never rewrite current/max HP or existing/max DP pools");
        Creature cannon = (Creature)H("Actor", Blank<HotaruContestCannon>(), null, Player)!;
        Creature other = (Creature)H("Actor", Blank<HotaruContestCannon>(), null, Player)!;
        Creature phantom = (Creature)H("Actor", Blank<TestSubject>(), null, Player)!;
        Set(Combat, "_allies", new List<Creature> { Hero, cannon, other, phantom });
        Power<HbrCalamityPower>(cannon, 3); Power<StrengthPower>(cannon, 999); Power<DexterityPower>(cannon, 999);
        Equal(InteractionStat(cannon, "Strength"), 394m, "cannon displayed Strength subtracts local Calamity before inherited final Crimson doubling and ignores local native Strength");
        Equal(InteractionStat(cannon, "Dexterity"), 478m, "cannon displayed Dexterity applies local Calamity exactly once before final stance");
        Equal(InteractionStat(other, "Strength"), 400m, "one cannon's local Calamity never contaminates another cannon's inherited attributes");
        decimal hpExpected = (decimal)H("Outgoing", 394m, 478m, 150m, false, 6m)!;
        Equal(InteractionCannon(cannon, () => InteractionConvert(Enemy, Hero)), hpExpected,
            "owner-sourced cannon actual HP coefficient matches its displayed locally debuffed attributes");
        var dp = Power<HighDimensionalShellPower>(Enemy, 1000);
        typeof(HighDimensionalShellPower).GetProperty("CurrentDpData", F)!.SetValue(dp, "1000");
        Equal(InteractionCannon(cannon, () => InteractionConvert(Enemy, Hero)), (decimal)H("Outgoing", 394m, 478m, 150m, true, 6m)!,
            "owner-sourced cannon DP route keeps its distinct Dexterity-heavy weights after Crimson and local Calamity");
        Powers(Enemy).Remove(dp);
        Creature happy = (Creature)H("Actor", Blank<HotaruHappinessCannon>(), null, Player)!;
        ((List<Creature>)typeof(CombatState).GetField("_allies", F)!.GetValue(Combat)!).Add(happy);
        Power<HbrCalamityPower>(happy, 3);
        typeof(Millsage).GetProperty("HappinessUpgradeLevel", F)!.SetValue(millsage, 3);
        Equal(InteractionCannon(happy, () => InteractionConvert(Enemy, Hero)), (decimal)H("Outgoing", 514m, 622m, 150m, false, 6m)!,
            "HappinessLv3 adds one extra positive40percent Strength contribution before owner/local Calamity and final Crimson doubling");
        Set(Hero.GetPower<StrengthPower>()!, "_amount", -20);
        Equal(InteractionCannon(happy, () => InteractionConvert(Enemy, Hero)), (decimal)H("Outgoing", 154m, 190m, 150m, false, 6m)!,
            "HappinessLv3 never duplicates a negative native Strength penalty under Crimson");
        Set(Hero.GetPower<StrengthPower>()!, "_amount", 20);
        Power<DexterityPower>(Enemy, 3); H("Act", 1);
        Equal(InteractionCannon(happy, () => InteractionConvert(Enemy, Hero)), (decimal)H("Outgoing", 508m, 616m, 210m, false, 6m)!,
            "enemy Dexterity's fixed six-point ignore remains after final Crimson rather than being doubled as an allied percentage");
        H("Act", 0); Powers(Enemy).Clear();
        Set(cannon.GetPower<HbrCalamityPower>()!, "_amount", 500);
        Equal(InteractionStat(cannon, "Strength"), 0m, "large local Calamity clamps before Crimson without reviving zero cannon Strength");
        Equal(InteractionCannon(cannon, () => InteractionConvert(Enemy, Hero)), 1m,
            "cannon actual coefficient obeys the same local zero-stat clamp and minimum damage as its displayed attributes");
        Set(cannon.GetPower<HbrCalamityPower>()!, "_amount", 3);
        Power<PhantomPower>(phantom, 1); Power<StrengthPower>(phantom, -5);
        Equal(InteractionStat(phantom, "Strength"), 135m, "phantom uses only its own signed two-percent Strength rather than summoner Crimson or native Strength");
        Equal(InteractionStat(phantom, "Vitality"), 100m, "summoner Crimson and Sakura never rewrite an independent phantom's defensive stats");
        Power<CrimsonBladeFlowPower>(phantom, 2); Power<HbrCalamityPower>(phantom, 2);
        Equal(InteractionStat(phantom, "Strength"), 266m, "a phantom's own Crimson applies after its own negative Strength and Calamity");
        Equal(InteractionStat(phantom, "Spirit"), 0m, "a phantom's own Crimson zeroes only its own defense");
        Powers(Hero).Remove(crimson);
        Equal(InteractionStat(Hero, "Vitality"), 130m, "removing Crimson restores unmultiplied Sakura40percent minus owner Calamity10");
        Equal(InteractionStat(Hero, "Spirit"), 130m, "Sakura's real40percent defensive bonus remains unchanged by native stat layer scaling");
        Power<ReptureDualSwordsPower>(Hero, 1); InteractionRune = true;
        Equal(InteractionCannon(cannon, () => InteractionConvert(Enemy, Hero)), hpExpected,
            "virtual-rune cannon attack keeps exactly the real Crimson local-Calamity order without needing a Crimson power");
    }
    static async Task InteractionTemporaryPowerCases()
    {
        InteractionReset(); Power<StrengthPower>(Hero, -3); Power<DexterityPower>(Hero, -4);
        await PowerCmd.Apply<FlexPotionPower>(new ThrowingPlayerChoiceContext(), Hero, 5m, Hero, null);
        await PowerCmd.Apply<SpeedPotionPower>(new ThrowingPlayerChoiceContext(), Hero, 6m, Hero, null);
        Equal(InteractionStat(Hero, "Strength"), 104m, "real temporary Strength adds to signed permanent layers at two percentage points per layer");
        Equal(InteractionStat(Hero, "Intelligence"), 104m, "real temporary Dexterity adds to signed layers at two percentage points per layer");
        Power<CrimsonBladeFlowPower>(Hero, 2);
        Equal(InteractionStat(Hero, "Strength"), 208m, "Crimson final doubles temporary and permanent Strength exactly once");
        var flex = Hero.GetPower<FlexPotionPower>()!; var speed = Hero.GetPower<SpeedPotionPower>()!;
        await flex.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero });
        await speed.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero });
        Equal(InteractionStat(Hero, "Strength"), 188m, "native temporary Strength expiry restores the negative base under final Crimson");
        Equal(InteractionStat(Hero, "Intelligence"), 92m, "native temporary Dexterity expiry restores its signed two-percent base without changing unrelated stats");
    }
    static async Task InteractionNativeFocusedBlockCases()
    {
        foreach (bool hbr in new[] { true, false })
        foreach (decimal raw in hbr ? new[] { .639999m, .64m, .65m, 5m } : new[] { 15.999m, 16m, 16.1m, 500m })
        {
            InteractionReset(hbr); Power<ReptureBowPower>(Hero, 1); Power<FocusedShotPower>(Hero, 1);
            var card = InteractionOwned<DefendIronclad>(); var play = Play(card);
            decimal preview = Hook.ModifyBlock(Combat, Hero, raw, ValueProp.Move, card, play, out _);
            decimal granted = await CreatureCmd.GainBlock(Hero, raw, ValueProp.Move, play, true);
            int actual = Hero.Block;
            int expected = (int)decimal.Floor(actual / (hbr ? 48m : 12m));
            Equal(granted, preview, "real native block command receives the same post-form amount as preview at raw " + raw + " HBR=" + hbr);
            Equal(Hero.GetPowerAmount<StrengthPower>(), expected,
                "native Block-to-AfterBlockGained-to-PowerCmd chain floors complete " + (hbr ? "48" : "12") + "-point Strength intervals at raw " + raw);
            if (hbr) Equal(InteractionStat(Hero, "Strength"), 100m + expected * 2m,
                "actual FocusedShot Strength layers immediately affect live attributes at two percent");
        }
        InteractionReset(); Power<ReptureBowPower>(Hero, 1); Power<FocusedShotPower>(Hero, 1);
        Power<CrimsonBladeFlowPower>(Hero, 2); Power<DexterityPower>(Hero, 3); Power<StrengthPower>(Hero, -3);
        decimal combined = await CreatureCmd.GainBlock(Hero, 5m, ValueProp.Move, Play(InteractionOwned<DefendIronclad>()), true);
        Equal(combined, 795m, "actual block combines native Dexterity's6percent Wisdom, Focused75percent and Crimson'stwofold block once each");
        Equal(Hero.GetPowerAmount<StrengthPower>(), 13m, "Focused converts combined795block into sixteen layers on top of signed minus-three Strength");
        Equal(InteractionStat(Hero, "Strength"), 252m, "Focused's actual signed Strength result enters the final Crimson attribute transformation once");
        InteractionReset(); Power<ReptureDualSwordsPower>(Hero, 1); Power<FocusedShotPower>(Hero, 1); InteractionRune = true;
        await CreatureCmd.GainBlock(Hero, 5m, ValueProp.Move, Play(InteractionOwned<DefendIronclad>()), true);
        Check(Hero.Block == 1000 && Hero.GetPowerAmount<StrengthPower>() == 0,
            "virtual Crimson's doubled block does not bypass FocusedShot's authored bow-form requirement");
    }
}
