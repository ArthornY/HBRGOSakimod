using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

static class GardenUpgradeStandalone
{
    public static void Run(string reportPath) => EffectChecks.RunGardenUpgrades(reportPath);
}

static partial class EffectChecks
{
    static readonly Type[] GardenCards = [typeof(MyLegendBeginsHbrgoSaki), typeof(HelloWorldHbrgoSaki),
        typeof(SaviorGrandEntranceHbrgoSaki), typeof(MoreThanOneTruthHbrgoSaki),
        typeof(CrushYouLikeABugHbrgoSaki), typeof(WorldsFinestHbrgoSaki)];
    sealed record GardenAttack(CardModel Card, decimal Raw, bool All, bool Eye, bool Critical, bool Medium);
    static readonly List<GardenAttack> GardenAttacks = [];
    static readonly List<Creature> GardenStuns = [];
    static bool GardenAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        GardenAttacks.Add(new((CardModel)__instance.ModelSource!,
            (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(__instance)!,
            __instance.IsMultiTargeted, Hero.GetPower<MindsEyePower>() != null,
            Hero.GetPower<HbrCriticalRateUpPower>() != null, Hero.GetPower<HbrComboMediumPower>() != null));
        __result = Task.FromResult(__instance); return false;
    }
    static void GardenReset()
    {
        FormReset(); GardenAttacks.Clear(); GardenStuns.Clear();
        Set(RunState, "_players", new List<MegaCrit.Sts2.Core.Entities.Players.Player> { Player });
    }
    static T GardenCard<T>(params int[] ids) where T : CardModel, new()
    {
        T card = FormCard<T>();
        foreach (int id in ids) GardenUpgrade(card, id);
        return card;
    }
    static void GardenUpgrade(CardModel card, int id)
    {
        Check((bool)Native(card, "PrepareUpgradeEffect", id)!, "garden choice is eligible: " + card.GetType().Name + "/" + id);
        card.UpgradeInternal(); card.FinalizeUpgradeInternal();
    }
    static void GardenPermanent(CardModel card)
    {
        card.Pile?.RemoveInternal(card, true); Player.Deck.AddInternal(card);
    }
    static Task GardenPlay(CardModel card)
    {
        var play = Play(card); Set(play, "<Target>k__BackingField", Enemy);
        return (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), play)!;
    }
    static void GardenChoices()
    {
        foreach (Type type in GardenCards)
        {
            GardenReset();
            var card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(card, "_owner", Player);
            int candidates = type == typeof(SaviorGrandEntranceHbrgoSaki) ? 8 : 7;
            Check(((IReadOnlyList<int>)Native(card, "GetAvailableUpgradeEffects")!).Count == candidates,
                "garden starts with complete independent candidate list: " + type.Name);
            object[] catalog = ((System.Collections.IEnumerable)Call("HbrMemoryGardenUpgradeCatalog", "GetAllUpgradeSkills", card)!).Cast<object>().ToArray();
            Equal(catalog.Length, candidates, "garden skill browser exposes all candidates independently of the seven-upgrade cap: " + type.Name);
            if (type == typeof(WorldsFinestHbrgoSaki))
                foreach (object entry in catalog)
                {
                    int id = (int)entry.GetType().GetProperty("EffectId")!.GetValue(entry)!;
                    string description = (string)entry.GetType().GetProperty("Description")!.GetValue(entry)!;
                    Check(description == (string)Call("HbrMemoryGardenUpgradeCatalog", "GetEffectDescription", card, id.ToString())!,
                        "Tama skill browser has no obsolete revival-tier prerequisite for choice " + id);
                }
            // Reverse order proves there are no residual prerequisites or cost/X exclusions.
            foreach (int id in Enumerable.Range(0, candidates).Reverse().Take(7)) GardenUpgrade(card, id);
            Check(card.CurrentUpgradeLevel == 7 && card.MaxUpgradeLevel == 7 && !card.IsUpgradable,
                "seven choices reach the exact upgrade cap: " + type.Name);
            int remaining = ((IReadOnlyList<int>)Native(card, "GetAvailableUpgradeEffects")!).Count;
            Check(remaining == (candidates == 8 ? 1 : 0), "unselected candidates remain distinct from upgrade cap: " + type.Name);
        }
    }
    static void GardenLocalizationCases()
    {
        foreach (Type type in GardenCards)
        {
            GardenReset();
            var card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(card, "_owner", Player);
            Player.PlayerCombatState!.Hand.AddInternal(card, -1, true);
            string face = DesuwaFace(card, false);
            Check(!face.Contains('{') && !face.Contains("倍率"),
                "fresh garden card face renders numeric effects without unresolved keys: " + type.Name);
            int candidates = type == typeof(SaviorGrandEntranceHbrgoSaki) ? 8 : 7;
            foreach (int id in Enumerable.Range(0, candidates))
            {
                string text = (string)Call("HbrMemoryGardenUpgradeCatalog", "GetEffectDescription", card, id.ToString())!;
                Check(!text.Contains('{') && !text.StartsWith(card.Id.Entry),
                    "garden upgrade description formats all numeric and energy-icon arguments: " + type.Name + "/" + id);
            }
            foreach (int id in Enumerable.Range(0, candidates).Take(7)) GardenUpgrade(card, id);
            Check(!DesuwaFace(card, false).Contains('{'), "fully upgraded garden card face formats every appended effect: " + type.Name);
        }
    }
    static async Task GardenLegendCases()
    {
        GardenReset(); var card = GardenCard<MyLegendBeginsHbrgoSaki>(3, 5, 6);
        await GardenPlay(card);
        var attack = GardenAttacks.Single();
        Check(attack.Eye && !attack.Critical && !attack.Medium,
            "Legend real attack sees pre-attack Minds Eye but not its post-attack combo or critical grants");
        Check(Hero.GetPower<HbrComboMediumPower>() is { Amount: 3 } &&
            Hero.GetPower<HbrCriticalRateUpPower>() is { Amount: 3 } && Hero.GetPower<HbrCriticalDamageUpPower>() is { Amount: 3 },
            "Legend real body grants medium combo and both three-turn critical effects after attack");
        Equal(attack.Raw, 7m, "Legend preserves its seven-unit skill basis");
        GardenReset(); card = GardenCard<MyLegendBeginsHbrgoSaki>(0, 1, 2);
        await GardenPlay(card);
        Check(GardenAttacks.Single().All && Hero.GetPower<VigorPower>() is { Amount: 5 },
            "Legend preserves all-target and five-Vigor upgrades");
        Check(Hero.GetPower<MyLegendNextAttackDoublePower>() != null,
            "Legend's own post-attack Vigor qualifies its doubled next-attack grant");
        GardenReset(); card = GardenCard<MyLegendBeginsHbrgoSaki>();
        Equal((int)Native(card, "GetHitCount", 7)!, 2m, "fixed Legend still uses two base hits");
        GardenUpgrade(card, 4); Equal((int)Native(card, "GetHitCount", 7)!, 7m, "X Legend uses actual resolved X as base hits");
        Check(card.EnergyCost.CostsX, "Legend X upgrade replaces native energy mode");
    }
    static async Task GardenHelloCases()
    {
        foreach (int block in new[] { 1000, 1550 })
        {
            GardenReset(); var card = GardenCard<HelloWorldHbrgoSaki>(4);
            SetNativeBlock(block); await GardenPlay(card);
            Check(Hero.Block == 0, "Hello World actually loses all initial block " + block);
            Equal(Hero.GetPower<HotaruDamageAmplificationPower>()?.DamagePercent ?? 0m, block / 100m,
                "Hello World preserves exact one-percent per hundred block " + block);
        }
    }
    static async Task GardenSaviorCases()
    {
        GardenReset(); var card = GardenCard<SaviorGrandEntranceHbrgoSaki>(5, 6, 7);
        await GardenPlay(card);
        var permanent = Enemy.Powers.Where(power => power is HbrDefenseDownPower or FragilePower).ToArray();
        Check(permanent.Length == 2 && permanent.All(power =>
            power.GetType().GetProperty("CurrentEffectProfile", F)!.GetValue(power)!.ToString()!.Contains("Eternal")),
            "Savior creates separate eternal Fragile and Defense Down instances");
        Check(permanent.All(power => (bool)power.GetType().GetProperty("HbrUnlimitedInstances", F)!.GetValue(power)!),
            "Savior's eternal debuffs do not occupy normal slots");
        Check(permanent.All(power => power.Description.GetFormattedText().Contains("不计入") &&
            power.Description.GetFormattedText().Contains("整场战斗")),
            "Savior permanent debuff tooltips explicitly identify combat duration and uncapped instances");
        decimal defense = (decimal)typeof(HbrPercentDebuffPower).GetProperty("EffectPercent", F)!.GetValue(permanent.OfType<HbrDefenseDownPower>().Single())!;
        decimal expected = (decimal)Call("HbrEffectFormula", "Debuff", 40m, Hero, Enemy,
            Helper("HbrEffectFormula").GetField("IntelligenceLuck", F)!.GetValue(null))!;
        Equal(defense, expected, "Savior's own Sea Breeze doubles permanent Defense Down basis before attribute scaling");
        MethodInfo ordinary = Helper("HbrPercentDebuffApplication").GetMethods(F).Single(method =>
            method.Name == "ApplyDefenseDown" && method.GetParameters()[1].ParameterType == typeof(Creature));
        foreach (decimal modifiers in new[] { 1m, 2m, 3m })
            await (Task)Invoke(ordinary, null, new ThrowingPlayerChoiceContext(), Enemy, modifiers, Hero, card, false)!;
        Check(Enemy.Powers.OfType<HbrDefenseDownPower>().Count() == 3 && Enemy.Powers.Contains(permanent.OfType<HbrDefenseDownPower>().Single()),
            "third ordinary Defense Down replaces an ordinary slot and preserves Savior's uncapped permanent slot");
        GardenReset(); card = GardenCard<SaviorGrandEntranceHbrgoSaki>(3); await GardenPlay(card);
        var power = Hero.GetPower<SaviorGrandEntrancePower>()!;
        Check((bool)Native(power, "TryTakeAllEnemyStun")! && !(bool)Native(power, "TryTakeAllEnemyStun")!,
            "Savior all-enemy stun can be reserved only once in one owner turn");
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2);
        Check((bool)Native(power, "TryTakeAllEnemyStun")!, "Savior all-enemy stun resets in the next owner turn");
        GardenReset(); card = GardenCard<SaviorGrandEntranceHbrgoSaki>(0); await GardenPlay(card);
        foreach (Type type in GardenCards)
        {
            var source = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(source, "_owner", Player); Player.Deck.AddInternal(source);
        }
        Equal((int)Call("SaviorGrandEntranceCombatHelper", "GetRawCriticalMinimumReduction", Hero)!, 6,
            "Savior critical interval reduction counts six distinct permanent 31A styles");
        var duplicate = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(HelloWorldHbrgoSaki))).MutableClone();
        Set(duplicate, "_owner", Player); Player.Deck.AddInternal(duplicate);
        Equal((int)Call("SaviorGrandEntranceCombatHelper", "GetRawCriticalMinimumReduction", Hero)!, 6,
            "duplicate 31A cards do not increase Savior's critical extension beyond six");
    }
    static bool GardenStunLeaf(Creature creature, ref Task __result)
    {
        Set(creature.Monster!, "<NextMove>k__BackingField", new MoveState("STUNNED", _ => Task.CompletedTask));
        GardenStuns.Add(creature); __result = Task.CompletedTask; return false;
    }
    static void GardenRawReceipt(DamageResult result)
    {
        Type helper = Helper("SaviorGrandEntranceCombatHelper");
        Type record = helper.GetNestedType("RawReceipt", F)!;
        object receipt = Activator.CreateInstance(record, F, null, new object[] { Hero }, null)!;
        object table = helper.GetField("RawReceipts", F)!.GetValue(null)!;
        table.GetType().GetMethod("Add")!.Invoke(table, new[] { (object)result, receipt });
    }
    static async Task GardenSaviorStunCases()
    {
        GardenReset(); var card = GardenCard<SaviorGrandEntranceHbrgoSaki>(3, 7); await GardenPlay(card);
        var sibling = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!;
        Set(Combat, "_enemies", new List<Creature> { Enemy, sibling });
        var power = Hero.GetPower<SaviorGrandEntrancePower>()!;
        var hit = Blank<DamageResult>(); Set(hit, "<Receiver>k__BackingField", Enemy);
        GardenRawReceipt(hit);
        await (Task)Native(power, "AfterDamageGiven", new ThrowingPlayerChoiceContext(), Hero, hit, ValueProp.Move, Enemy, card)!;
        Check(GardenStuns.Count == 2 && Enemy.IsStunned && sibling.IsStunned,
            "Savior first critical receipt actually requests all-enemy stun once");
        Check(Enemy.GetPower<HbrDefenseDownPower>() is { Amount: 2 } && sibling.GetPower<HbrDefenseDownPower>() is { Amount: 2 },
            "Savior Hard Blade applies ordinary two-turn Defense Down for each all-enemy stun");
        var later = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!;
        Set(Combat, "_enemies", new List<Creature> { Enemy, sibling, later });
        hit = Blank<DamageResult>(); Set(hit, "<Receiver>k__BackingField", later); GardenRawReceipt(hit);
        await (Task)Native(power, "AfterDamageGiven", new ThrowingPlayerChoiceContext(), Hero, hit, ValueProp.Move, later, card)!;
        Check(GardenStuns.Count == 3 && later.IsStunned,
            "Savior later critical in the same turn requests only its own target stun");
        Check(later.GetPower<HbrDefenseDownPower>() is { Amount: 2 },
            "Savior Hard Blade also follows the ordinary target-only critical stun");
        GardenReset(); card = GardenCard<SaviorGrandEntranceHbrgoSaki>(7); await GardenPlay(card);
        await CreatureCmd.Stun(Enemy);
        await (Task)Call("SaviorGrandEntranceCombatHelper", "OnEnemyStunned", new ThrowingPlayerChoiceContext(), Hero, Enemy, null)!;
        Check(Enemy.GetPower<HbrDefenseDownPower>() is { Amount: 2 },
            "Savior Hard Blade accepts the 31A cut-in's attributed stun without a card source");
    }
    static async Task GardenTruthCases()
    {
        GardenReset(); var first = GardenCard<MoreThanOneTruthHbrgoSaki>();
        for (int i = 0; i < 3; i++) await GardenPlay(first);
        Check(Hero.Powers.OfType<HbrAttackUpPower>().Count() == 3 && Hero.GetPower<StrengthPower>() == null,
            "Truth actual card body grants an independent unlimited Attack Up per play instead of Strength");
        foreach (var power in Hero.Powers.OfType<HbrAttackUpPower>())
        {
            string profile = power.GetType().GetProperty("CurrentEffectProfile", F)!.GetValue(power)!.ToString()!;
            Check(profile.Contains("None") && profile.Contains("Eternal"), "Truth grant lasts for combat and occupies no ordinary slot");
            Check(power.Description.GetFormattedText().Contains("不计入") && power.Description.GetFormattedText().Contains("整场战斗"),
                "Truth permanent Attack Up tooltip explicitly describes its combat duration and uncapped instance");
        }
        GardenReset(); var truth = GardenCard<MoreThanOneTruthHbrgoSaki>(2, 5); GardenPermanent(truth);
        Set(Player.PlayerCombatState!, "_energy", 0);
        await (Task)Call("MoreThanOneTruthCombatHelper", "OnExtraTurnEntered", new ThrowingPlayerChoiceContext(), Player)!;
        Equal(Player.PlayerCombatState!.Energy, 1m, "Truth 1MORE gives exactly one energy per entry callback");
        Check(Hero.GetPower<HbrComboSmallPower>() is { Amount: 5 }, "Truth Nightmare gives the shared five-use Small combo");
    }
    static async Task GardenKarenCases()
    {
        GardenReset(); var card = GardenCard<CrushYouLikeABugHbrgoSaki>(1, 4, 6);
        await GardenPlay(card);
        Check(GardenAttacks.Single().Critical && Hero.GetPower<MindsEyePower>() == null,
            "Karen grants critical rate/damage before the actual attack and no longer grants Minds Eye");
        Equal((decimal)Call("CrushYouLikeABugCombatHelper", "GetDpDamageMultiplier", card, Enemy, ValueProp.Move)!, 1m,
            "Karen without DP or Break has no situational multiplier");
        Power<BreakPower>(Enemy, 1);
        Equal((decimal)Call("CrushYouLikeABugCombatHelper", "GetDpDamageMultiplier", card, Enemy, ValueProp.Move)!, 1.5m,
            "Karen Bloody Burning increases damage only against this Break target");
        Equal((decimal)Call("CrushYouLikeABugCombatHelper", "GetBreakDestructionMultiplier", card, Enemy)!, 2m,
            "Karen Bloody Burning doubles only the Break target's destruction gain");
        object?[] cost = [card, 1m, null];
        Check((bool)Invoke(typeof(CrushYouLikeABugHbrgoSaki).GetMethod("TryModifyEnergyCostInCombat", F)!, card, cost)! && (decimal)cost[2]! == 0m,
            "Karen Best Position reduces cost to zero when any enemy is in Break");
        GardenUpgrade(card, 0); cost = [card, 0m, null];
        Invoke(typeof(CrushYouLikeABugHbrgoSaki).GetMethod("TryModifyEnergyCostInCombat", F)!, card, cost);
        Equal((decimal)cost[2]!, 0m, "Karen Best Position and Flash never produce negative energy cost");
        GardenReset(); card = GardenCard<CrushYouLikeABugHbrgoSaki>(5);
        Equal((int)Native(card, "GetHitCount", 0)!, 3m, "Karen Bloody Dance retains two extra attacks");
    }
    static async Task GardenTamaCases()
    {
        GardenReset(); var card = GardenCard<WorldsFinestHbrgoSaki>(1, 6); GardenPermanent(card);
        var duplicate = GardenCard<WorldsFinestHbrgoSaki>(1); GardenPermanent(duplicate);
        var talisman = Power<SpiritTalismanPower>(Enemy, 7);
        await GardenPlay(card);
        Check(Hero.GetPower<MindsEyePower>() != null && Hero.GetPower<HbrCriticalRateUpPower>() != null &&
            Hero.GetPower<HbrCriticalDamageUpPower>() != null, "Tama Open the Road grants new Minds Eye and both critical effects");
        var play = Play(card);
        await (Task)Call("WorldsFinestCombatHelper", "AfterThirtyOneACardPlayed", new ThrowingPlayerChoiceContext(), play)!;
        Equal(talisman.Amount, 9m, "Tama Frightening Shout counts itself once and duplicate permanent styles do not multiply its plus two");
        await (Task)Call("WorldsFinestCombatHelper", "AfterThirtyOneACardPlayed", new ThrowingPlayerChoiceContext(), play)!;
        Equal(talisman.Amount, 10m, "Tama Frightening Shout caps existing talismans at ten");
        Powers(Enemy).Clear();
        await (Task)Call("WorldsFinestCombatHelper", "AfterThirtyOneACardPlayed", new ThrowingPlayerChoiceContext(), play)!;
        Check(Enemy.GetPower<SpiritTalismanPower>() == null, "Tama Frightening Shout never creates a talisman on an unmarked enemy");
        GardenReset(); card = GardenCard<WorldsFinestHbrgoSaki>(2);
        var virus = Power<HbrVirusPower>(Hero, 2); var weak = Power<WeakPower>(Hero, 3);
        await (Task)Call("WorldsFinestCombatHelper", "CleanseOneDebuff", card)!;
        Check(!Hero.Powers.Contains(virus) && Hero.Powers.Contains(weak), "Tama Sun Scent preferentially removes Virus and preserves other debuffs");
        await (Task)Call("WorldsFinestCombatHelper", "CleanseOneDebuff", card)!;
        Check(!Hero.Powers.Contains(weak), "Tama Sun Scent removes another visible debuff when Virus is absent");
    }
    static bool GardenRemovePermanentLeaf(CardModel card, ref Task __result)
    {
        card.RemoveFromCurrentPile(); card.RemoveFromState();
        __result = Task.CompletedTask; return false;
    }
    static bool GardenHpWriteLeaf(Creature creature, decimal amount, ref Task __result)
    {
        Set(creature, "_currentHp", (int)amount); __result = Task.CompletedTask; return false;
    }
    static async Task GardenRevivalCase()
    {
        GardenReset(); var card = GardenCard<WorldsFinestHbrgoSaki>(0); GardenPermanent(card);
        var shield = Blank<DeflectorProtection>(); Set(shield, "<IsMutable>k__BackingField", true); Set(shield, "_owner", Player);
        Set(Player, "_relics", Player.Relics.Concat(new RelicModel[] { shield }).ToList());
        shield.EnsureDpInitialized(); shield.LoseDpByCurrentPercent(1m);
        Set(Hero, "_currentHp", 123);
        object scope = Call("WorldsFinestCombatHelper", "BeginDamage")!;
        try
        {
            Equal((decimal)Call("WorldsFinestCombatHelper", "PreventLethalHpLoss", Hero, 122m, HpLossHookPhase.AfterOsty)!, 122m,
                "Tama Rebirth leaves a nonlethal HP loss untouched");
            Equal((decimal)Call("WorldsFinestCombatHelper", "PreventLethalHpLoss", Hero, 123m, HpLossHookPhase.AfterOsty)!, 0m,
                "Tama Rebirth reserves the real permanent card and blocks lethal HP loss");
            var pending = (Array)Call("WorldsFinestCombatHelper", "TakePendingRevivals")!;
            Check(pending.Length == 1, "Tama Rebirth exposes exactly one reserved recovery");
            await (Task)Call("WorldsFinestCombatHelper", "FinishHpModifiers", Task.CompletedTask, pending)!;
            Check(Hero.CurrentHp == 1 && shield.Dp == shield.MaxDp,
                "Tama Rebirth completion sets HP to one and fills DP exactly");
            Check(card.HasBeenRemovedFromState && !Player.Deck.Cards.Contains(card),
                "Tama Rebirth completion permanently removes the reserved source card");
        }
        finally { Call("WorldsFinestCombatHelper", "CloseDamage", scope); }
    }
    static async Task GardenUpgradeCases()
    {
        foreach (Type type in GardenCards.Concat(new[] { typeof(MindsEyePower), typeof(HbrComboMediumPower), typeof(HbrComboSmallPower),
            typeof(HbrCriticalRateUpPower), typeof(HbrCriticalDamageUpPower), typeof(HbrAttackUpPower),
            typeof(HbrDefenseDownPower), typeof(SaviorGrandEntrancePower), typeof(MyLegendNextAttackPower),
            typeof(MyLegendNextAttackDoublePower), typeof(SpiritTalismanPower), typeof(HbrVirusPower), typeof(VigorPower), typeof(BreakPower), typeof(WeakPower), typeof(FragilePower), typeof(DestructionRatePower), typeof(DestructionRateCapIncreasePower) }))
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var h = new Harmony(ProductionId + ".garden-upgrades"); var patched = new List<MethodBase>();
        void Patch(MethodBase m, string prefix)
        { h.Patch(m, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!)); patched.Add(m); }
        try
        {
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            foreach (var wait in typeof(Cmd).GetMethods(F).Where(m => (m.Name is "Wait" or "CustomScaledWait") && m.ReturnType == typeof(Task)))
                Patch(wait, nameof(VisualTask));
            Patch(typeof(AttackCommand).GetMethod("Execute", F)!, nameof(GardenAttackLeaf));
            Patch(typeof(CreatureCmd).GetMethods(F).Single(method => method.Name == "Stun" && method.GetParameters().Length == 2), nameof(GardenStunLeaf));
            Patch(typeof(CardPileCmd).GetMethods(F).Single(method => method.Name == "RemoveFromDeck" &&
                method.GetParameters()[0].ParameterType == typeof(CardModel)), nameof(GardenRemovePermanentLeaf));
            Patch(typeof(CreatureCmd).GetMethods(F).Single(method => method.Name == "SetCurrentHp"), nameof(GardenHpWriteLeaf));
            h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.CardDescriptionLocPatch", true)!).Patch();
            patched.Add(AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Description)));
            foreach (string name in new[] { "HbrEffectApplicationPatch", "HbrFragileStackingPatch", "HbrFragileApplyPatch", "HbrFragileCopyPatch" })
                h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch();
            GardenChoices(); GardenLocalizationCases(); await GardenLegendCases(); await GardenHelloCases(); await GardenSaviorCases(); await GardenSaviorStunCases();
            await GardenTruthCases(); await GardenKarenCases(); await GardenTamaCases(); await GardenRevivalCase(); await CutinBondCases(); await MindsEyeCases(); await GardenNativeExtraTurnCases(); await YukiNativeCases();
        }
        finally
        {
            foreach (MethodBase m in patched.Distinct()) h.Unpatch(m, HarmonyPatchType.All, h.Id);
            CombatInProgress(false); As31O(false); Reset();
        }
    }
    public static void RunGardenUpgrades(string reportPath)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        try { Install(); Localization(); GardenUpgradeCases().GetAwaiter().GetResult(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            metadataDependencies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name == "System.IO.Hashing")
                .Select(assembly => new { path = assembly.Location, version = assembly.GetName().Version?.ToString(), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) }).ToArray(),
            boundary = "Actual six 31A card bodies, native model upgrades/costs and native PowerCmd application/removal; production common HBR formulas and instance policies. Attack execution, UI/texture/wait leaves isolated; Savior receipt-driven stuns isolate only native stun state mutation and inject a raw-critical receipt, then execute the real damage receipt/stun follow-up helpers; entry callback tested independently of multiplayer scheduling. Revival exercises the actual reservation/completion helpers with only terminal deck removal and HP-write commands isolated, and actual DP reset. Enumerable tests use the production typed callsite adapter into untouched native APIs and the actual native Potion of Binding caller; unknown external mod direct callers are outside this binding set. No in-game rendering or old-save migration checks." },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " 31A garden upgrade checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
