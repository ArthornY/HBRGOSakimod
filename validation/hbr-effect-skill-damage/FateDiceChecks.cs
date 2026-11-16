using System.Reflection;
using System.Text.Json;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

// 31O's magic sword switch and fate dice (2026-11-09; design v3,
// outputs/deck-rethink-31o-20261108/magic-sword-fate-dice-design.md). The production rules run on
// the host's player as 31O, with the fate dice record saved on the manual: swordheart from rolls,
// the critical range, crack penalties, kept swordheart, the fragments' roll effects and the manual's
// commands. The HUD button, the reward screen, the rest site and the manual page need a running game.
static partial class EffectChecks
{
    static Type RollRules => Mod.GetType("HBRGOSakiMod.Core.Models.Capabilities.RollKeywordRules", true)!;
    static Type FateRoll => Helper("HbrFateDiceRollHelper");
    static ElectronicSoldierManual FateManual => State<ElectronicSoldierManual>("Manual");

    const string WideEdge = "WIDE_EDGE", LastStand = "LAST_STAND", Floor = "FLOOR", FirstMove = "FIRST_MOVE", Reroll = "REROLL",
        SevenSeal = "SEVEN_SEAL", Mirror = "MIRROR", RoundNumber = "ROUND_NUMBER", WildDie = "WILD_DIE", AllIn = "ALL_IN",
        LuckyNumbers = "LUCKY_NUMBERS", HawkEye = "HAWK_EYE", SwordSong = "SWORD_SONG", Acceleration = "ACCELERATION",
        ProtagonistPrivilege = "PROTAGONIST_PRIVILEGE";

    static object? StaticCall(Type type, string method, params object?[] args) =>
        Invoke(type.GetMethods(F).Single(m => m.Name == method && m.GetParameters().Length == args.Length &&
            m.GetParameters().Zip(args).All(pair => pair.Second == null
                ? !pair.First.ParameterType.IsValueType
                : pair.First.ParameterType.IsInstanceOfType(pair.Second))), null, args);

    static object? ManualCall(string method, params object?[] args) =>
        Invoke(typeof(ElectronicSoldierManual).GetMethod(method, F)!, FateManual, args);

    static T ManualValue<T>(string property) => (T)typeof(ElectronicSoldierManual).GetProperty(property, F)!.GetValue(FateManual)!;

    static void Fate(string data) => typeof(ElectronicSoldierManual).GetProperty("HbrFateDiceData", F)!.SetValue(FateManual, data);

    static string FateJson(string[]? equipped = null, int cracks = 0, int bosses = 3, string[]? bag = null,
        int swordheart = 0, string[]? receipts = null) =>
        JsonSerializer.Serialize(new
        {
            Version = 1, Revision = 1, Swordheart = swordheart, BossesDefeated = bosses, Cracks = cracks,
            Equipped = equipped ?? Array.Empty<string>(), Bag = bag ?? Array.Empty<string>(),
            Receipts = receipts ?? Array.Empty<string>(), Claimed = Array.Empty<string>()
        });

    static object FateState => ManualValue<object>("FateDice");
    static T FateValue<T>(string property) => (T)FateState.GetType().GetProperty(property, F)!.GetValue(FateState)!;
    static string[] Equipped => FateValue<string[]>("Equipped");
    static string[] Bag => FateValue<string[]>("Bag");

    static void As31O(bool thirtyOneO) =>
        Set(Player, "<Character>k__BackingField", thirtyOneO ? Blank<HbrgoSaki31O>() : Blank<HbrgoSaki>());

    static void Equip(params string[] fragments) => Fate(FateJson(equipped: fragments));

    static int RegularSwordheart(int raw, int? final = null) =>
        (int)StaticCall(RollRules, "GetRegularSwordheartAmount", Player, new SoulRollBreakdown(raw, final ?? raw, "基础骰"))!;

    static int CriticalSwordheart() => (int)StaticCall(RollRules, "GetCriticalSwordheartAmount", Player)!;
    static int CriticalMinimum() => (int)StaticCall(RollRules, "GetCriticalMinimum", Player)!;
    static bool FateBool(string method, params object?[] args) => (bool)StaticCall(FateRoll, method, args)!;
    static int FateInt(string method, params object?[] args) => (int)StaticCall(FateRoll, method, args)!;

    static (int Draws, int SelfDamage, int Fragile) FateEffects(int raw, bool critical)
    {
        object effects = StaticCall(FateRoll, "EffectsOf", Player, raw, critical)!;
        int Read(string name) => (int)effects.GetType().GetProperty(name, F)!.GetValue(effects)!;
        return (Read("Draws"), Read("SelfDamageHits"), Read("FragileBursts"));
    }

    static void SetHp(int current) => Set(Hero, "_currentHp", current);

    static async Task FateDiceCases()
    {
        try
        {
            FateSwordheartCases();
            FateCriticalRangeCases();
            FateCrackCases();
            FateRollEffectCases();
            FateMirrorCases();
            FateKeptSwordheartCases();
            await FateCommandCases();
            FateStructureCases();
        }
        finally
        {
            CombatInProgress(false);
            SetHp(5000);
            Fate(string.Empty);
            As31O(false);
            Reset();
        }
    }

    static void FateSwordheartCases()
    {
        // Rolls: a raw multiple of seven gives one, a critical success two; the final roll does not count.
        Reset(); As31O(true); Fate(FateJson());
        Check(RegularSwordheart(7) == 1 && RegularSwordheart(14) == 1 && RegularSwordheart(77) == 1 && RegularSwordheart(98) == 1,
            "31O: raw 7, 14, 77 and 98 (multiples of seven) give one swordheart each");
        Check(RegularSwordheart(17) == 0 && RegularSwordheart(27) == 0 && RegularSwordheart(70 + 1) == 0,
            "31O: raw 17, 27 and 71 give none; a 7 in the number no longer counts without 七之刻印");
        Check(RegularSwordheart(20, 21) == 0 && RegularSwordheart(21, 30) == 1,
            "swordheart reads the raw roll: raw 20 modified to 21 gives none, raw 21 modified to 30 gives one");
        Check(CriticalMinimum() == 100 && RegularSwordheart(100) == 0 && CriticalSwordheart() == 2,
            "a critical success (raw 100) gives two swordheart instead of filling them, and no multiple-of-seven one on top");

        Equip(SevenSeal);
        Check(RegularSwordheart(17) == 1 && RegularSwordheart(27) == 1 && RegularSwordheart(14) == 2 && RegularSwordheart(77) == 2 &&
              RegularSwordheart(23) == 0,
            "七之刻印: raw 17 and 27 give one, raw 14 and 77 give two, raw 23 none");
        Fate(FateJson(bag: new[] { SevenSeal }));
        Check(RegularSwordheart(17) == 0 && RegularSwordheart(14) == 1, "七之刻印 in the bag does nothing");

        Equip(SwordSong);
        Check(CriticalSwordheart() == 4, "剑鸣: a critical success gives four swordheart");

        // Only 31O keeps swordheart and a fate die.
        As31O(false); Equip(SevenSeal, SwordSong);
        Check(RegularSwordheart(14) == 0 && CriticalSwordheart() == 0 && !ManualValue<bool>("UsesFateDice") &&
              CriticalMinimum() == 100,
            "the other Saki characters gain no swordheart from rolls and carry no fate die");
    }

    static void FateCriticalRangeCases()
    {
        Reset(); As31O(true);
        Equip(WideEdge);
        Check(CriticalMinimum() == 98, "宽刃: the critical range starts at raw 98");
        Equip(WildDie);
        Check(CriticalMinimum() == 95, "狂骰: the critical range starts at raw 95");
        Equip(WideEdge, WildDie);
        Check(CriticalMinimum() == 93 && RegularSwordheart(98) == 0, "宽刃 and 狂骰 add up to raw 93; raw 98 is then a critical success");
        Equip(LastStand);
        SetHp(2500);
        Check(CriticalMinimum() == 100, "绝境 at exactly half HP (2500/5000) leaves the range at 100");
        SetHp(2499);
        Check(CriticalMinimum() == 95, "绝境 below half HP (2499/5000) starts the range at raw 95");
        SetHp(5000);
        Fate(FateJson(bag: new[] { WideEdge, WildDie, LastStand }));
        Check(CriticalMinimum() == 100, "range fragments in the bag do nothing");
    }

    static void FateCrackCases()
    {
        Reset(); As31O(true);
        // Two slots before any boss; each opening of the magic sword cracks one, empty slots first.
        Fate(FateJson(equipped: new[] { WideEdge, WildDie }, bosses: 0));
        Check(FateValue<int>("Slots") == 2 && FateValue<int>("Capacity") == 2 && CriticalMinimum() == 93,
            "two slots, both fragments fitted");
        ManualCall("AddFateDiceCrack");
        Check(FateValue<int>("Cracks") == 1 && Equipped.SequenceEqual(new[] { WideEdge }) && Bag.SequenceEqual(new[] { WildDie }) &&
              CriticalMinimum() == 98,
            "a crack on a full set pushes the last fitted fragment (狂骰) back to the bag");
        ManualCall("AddFateDiceCrack");
        Check(FateValue<int>("Capacity") == 0 && Equipped.Length == 0 && Bag.Length == 2 && FateInt("RawPenalty", Player) == 0,
            "two cracks fill both slots; no roll penalty yet");
        ManualCall("AddFateDiceCrack");
        Check(FateInt("RawPenalty", Player) == 3 && FateInt("ApplyRawPenalty", Player, 50) == 47,
            "a third crack lowers raw rolls by 3 (50 becomes 47)");
        ManualCall("AddFateDiceCrack"); ManualCall("AddFateDiceCrack");
        Check(FateInt("RawPenalty", Player) == 9 && FateInt("ApplyRawPenalty", Player, 50) == 41 && FateInt("ApplyRawPenalty", Player, 5) == 1,
            "five cracks on two slots: raw −9, never below 1");

        // 修骰子: one crack per rest; fragments stay in the bag until the player fits them again.
        Type repair = Mod.GetType("HBRGOSakiMod.Core.Nodes.RestSite.FateDiceRepairRestSiteOption", true)!;
        Check((bool)StaticCall(repair, "ShouldOffer", Player)! && ManualValue<bool>("CanRepairFateDice"),
            "the rest site offers 修骰子 while the dice has a crack");
        Check((bool)ManualCall("RepairFateDiceCrack")! && FateValue<int>("Cracks") == 4 && FateInt("RawPenalty", Player) == 6,
            "修骰子 removes one crack (raw −9 becomes −6)");
        for (int i = 0; i < 4; i++) ManualCall("RepairFateDiceCrack");
        Check(FateValue<int>("Cracks") == 0 && !(bool)ManualCall("RepairFateDiceCrack")! &&
              !(bool)StaticCall(repair, "ShouldOffer", Player)! && Equipped.Length == 0 && Bag.Length == 2,
            "with no crack left 修骰子 is not offered; the fragments wait in the bag");

        // Slots grow with bosses beaten, up to five; a run from before the fate dice counts the acts behind it.
        Fate(FateJson(bosses: 7));
        Check(FateValue<int>("Slots") == 5, "slots stop at five");
        Fate(string.Empty);
        Set(RunState, "_currentActIndex", 2);
        Check(FateValue<int>("Slots") == 4 && FateValue<int>("Cracks") == 0 && Equipped.Length == 0,
            "an old save in act 3 reads as four empty slots (two bosses behind it)");
        Set(RunState, "_currentActIndex", 0);
        Check(FateValue<int>("Slots") == 2, "an old save in act 1 reads as two empty slots");
    }

    static void FateRollEffectCases()
    {
        Reset(); As31O(true);
        Equip(RoundNumber);
        Check(FateEffects(30, false).Draws == 1 && FateEffects(100, true).Draws == 1 && FateEffects(31, false).Draws == 0,
            "整数: raw 30 and 100 draw a card, raw 31 does not");
        Equip(WildDie);
        Check(FateEffects(10, false).SelfDamage == 1 && FateEffects(1, false).SelfDamage == 1 && FateEffects(11, false).SelfDamage == 0,
            "狂骰: raw 10 or less hurts the player once, raw 11 does not");
        Equip(HawkEye);
        Check(FateEffects(100, true).Fragile == 0, "鹰眼 without the bow form gives no Fragile");
        Power<ReptureBowPower>(Hero, 1);
        Check(FateEffects(100, true).Fragile == 1 && FateEffects(99, false).Fragile == 0,
            "鹰眼 in bow form: a critical success gives every enemy Fragile, other rolls do not");

        Reset(); As31O(true);
        Equip(Mirror);
        Check(FateBool("DoublesValues", Player, 44) && FateBool("DoublesValues", Player, 11) && FateBool("DoublesValues", Player, 99) &&
              !FateBool("DoublesValues", Player, 45) && !FateBool("DoublesValues", Player, 100),
            "镜面 triggers on raw 11, 44 and 99, not on 45 or 100");
        Equip(Floor);
        Check(FateBool("AppliesFloor", Player, 19) && FateBool("AppliesFloor", Player, 1) && !FateBool("AppliesFloor", Player, 20),
            "托底 lifts raw rolls under 20");
        Equip(LuckyNumbers);
        Check(!FateBool("AppliesLuckyNumbers", Player, 77), "幸运数字 needs the dual swords form");
        Power<ReptureDualSwordsPower>(Hero, 1);
        Check(FateBool("AppliesLuckyNumbers", Player, 7) && FateBool("AppliesLuckyNumbers", Player, 77) &&
              !FateBool("AppliesLuckyNumbers", Player, 17),
            "幸运数字 in dual swords form: raw 7 and 77 count as critical successes, 17 does not");
        Fate(FateJson());
        Check(!FateBool("AppliesLuckyNumbers", Player, 77), "without the fragment the dual swords form no longer makes 7/77 critical");

        Equip(Acceleration, AllIn, ProtagonistPrivilege);
        var percents = ((System.Collections.IEnumerable)StaticCall(FateRoll, "PercentModifiers", Player)!).Cast<object>()
            .Select(row => ((int)row.GetType().GetField("Item1")!.GetValue(row)!, (string)row.GetType().GetField("Item2")!.GetValue(row)!))
            .ToList();
        Check(percents.SequenceEqual(new[] { (20, "加速（命运碎片）"), (15, "孤注（命运碎片）"), (-10, "主人公特典（命运碎片）") }) &&
              FateBool("BlocksAdvantage", Player),
            "加速 +20%, 孤注 +15% and 主人公特典 −10% join the roll's modifiers; 孤注 takes no advantage die");

        // 再来 once per combat, 先手 once per round.
        Reset(); As31O(true);
        Equip(Reroll, FirstMove);
        Check(!FateBool("TryUseReroll", Player, 11) && FateBool("TryUseReroll", Player, 10) && !FateBool("TryUseReroll", Player, 3),
            "再来: raw 11 is kept; the first raw roll of 10 or less rerolls once, the next one does not");
        FieldInfo round = typeof(CombatState).GetFields(F).Single(field => field.Name.Contains("RoundNumber", StringComparison.OrdinalIgnoreCase));
        object before = round.GetValue(Combat)!;
        try
        {
            // Round numbers no other case uses, so an earlier roll cannot have marked them.
            round.SetValue(Combat, 9001);
            Check(FateBool("TakeFirstRollOfTurn", Player) && !FateBool("TakeFirstRollOfTurn", Player),
                "先手: the first roll of a round gains a die, the second does not");
            round.SetValue(Combat, 9002);
            Check(FateBool("TakeFirstRollOfTurn", Player), "先手: the next round's first roll gains a die again");
        }
        finally { round.SetValue(Combat, before); }
    }

    // A card's roll pipeline on the host: the card's capability with its state reset, as
    // a card gets it, without the registry (the host registers no Roll keyword).
    static RollGlossaryCapability RollPipeline(CardModel card)
    {
        var pipeline = Blank<RollGlossaryCapability>();
        Set(pipeline, "<IsMutable>k__BackingField", true);
        Set(pipeline, "<Owner>k__BackingField", card);
        FieldInfo states = typeof(RollGlossaryCapability).GetField("_suspendedRollStates", F)!;
        states.SetValue(pipeline, Activator.CreateInstance(states.FieldType));
        Invoke(typeof(RollGlossaryCapability).GetMethod("ResetCurrentRollState", F)!, pipeline);
        return pipeline;
    }

    // What a card's roll does once the dice have landed on this raw roll: the production
    // SetCurrentRollValueMultipliers, which both the card roll and each damage segment call.
    static void LandRoll(RollGlossaryCapability pipeline, CardModel card, int raw)
    {
        Set(pipeline, "_isCardRollActive", true);
        Set(pipeline, "_hasRolledForCurrentPlay", true);
        Set(pipeline, "_currentRoll", raw);
        Set(pipeline, "_currentRollDetails", new SoulRollBreakdown(raw, raw, "基础骰"));
        Invoke(typeof(RollGlossaryCapability).GetMethod("SetCurrentRollValueMultipliers", F)!, pipeline, card);
    }

    static decimal RollField(RollGlossaryCapability pipeline, string field) =>
        (decimal)typeof(RollGlossaryCapability).GetField(field, F)!.GetValue(pipeline)!;

    // 镜面 doubles a card's damage and block, not the power amounts the card gives
    // (「全局统筹」 2026-11-09: 「镜面的 ×2 只算进伤害和格挡，不算进给予的能力层数」).
    static void FateMirrorCases()
    {
        Reset(false); As31O(true); Equip(Mirror);
        var attack = Owned<StrikeIronclad>();
        var skill = Owned<DefendIronclad>();
        RollGlossaryCapability attackRoll = RollPipeline(attack), skillRoll = RollPipeline(skill);
        var strength = Power<StrengthPower>(Hero, 1);
        decimal tier = (decimal)StaticCall(RollRules, "GetAttackOrSkillValueMultiplier", Player,
            new SoulRollBreakdown(77, 77, "基础骰"), CardType.Attack)!;
        Check(tier == 1.2m, $"raw 77 raises a card's values by its tier (×{tier})");
        decimal Damage() => attackRoll.ModifyDamageMultiplicative(Enemy, 6m, ValueProp.Move, Hero, attack);
        decimal Block() => skillRoll.ModifyBlockMultiplicative(Hero, 5m, ValueProp.Move, skill, null);
        decimal Given(RollGlossaryCapability pipeline, CardModel card) =>
            pipeline.ModifyPowerAmountGivenMultiplicative(strength, Hero, 2m, Hero, card);

        LandRoll(attackRoll, attack, 77);
        LandRoll(skillRoll, skill, 77);
        Check(Damage() == 2.4m && Block() == 2.4m,
            $"镜面 on raw 77: the attack's damage ×{Damage()} and the skill's block ×{Block()}, the tier doubled");
        Check(Given(attackRoll, attack) == 1.2m && Given(skillRoll, skill) == 1.2m,
            $"镜面 on raw 77: the Strength the attack and the skill give stays at the tier (×{Given(attackRoll, attack)}, ×{Given(skillRoll, skill)})");
        LandRoll(attackRoll, attack, 76);
        LandRoll(skillRoll, skill, 76);
        Check(Damage() == 1.2m && Block() == 1.2m && Given(attackRoll, attack) == 1.2m,
            "raw 76 (same tier, not a repdigit): damage, block and given amounts all at the tier");
        Fate(FateJson());
        LandRoll(attackRoll, attack, 77);
        LandRoll(skillRoll, skill, 77);
        Check(Damage() == 1.2m && Block() == 1.2m, "without 镜面 fitted raw 77 is not doubled");

        // A card played inside this one suspends the roll and restores it afterwards; the
        // doubling goes and comes back with it, and a reset clears it.
        Equip(Mirror);
        LandRoll(attackRoll, attack, 77);
        Invoke(typeof(RollGlossaryCapability).GetMethod("BeginCardRollState", F)!, attackRoll);
        Check(RollField(attackRoll, "_currentMirrorMultiplier") == 1m && RollField(attackRoll, "_currentValueMultiplier") == 1m,
            "a card rolled inside this one starts without the outer card's 镜面");
        Invoke(typeof(RollGlossaryCapability).GetMethod("FinishCardRollState", F)!, attackRoll);
        Check(RollField(attackRoll, "_currentMirrorMultiplier") == 2m && Damage() == 2.4m && Given(attackRoll, attack) == 1.2m,
            "afterwards the outer card's roll and 镜面 are back: damage ×2.4, given amounts ×1.2");
        Invoke(typeof(RollGlossaryCapability).GetMethod("ResetCurrentRollState", F)!, attackRoll);
        Check(RollField(attackRoll, "_currentMirrorMultiplier") == 1m && Damage() == 1m, "resetting the roll state clears 镜面 too");
    }

    static void FateKeptSwordheartCases()
    {
        Reset(); As31O(true); Fate(FateJson());
        Check(SwordheartPower.GetKeptMaximum(Hero) == 7 && SwordheartPower.GetMaxAmount(Hero) == 7,
            "31O keeps up to seven swordheart");
        Power<SwordheartClarityPower>(Hero, 1);
        Check(SwordheartPower.GetKeptMaximum(Hero) == 7 && SwordheartPower.GetMaxAmount(Hero) == 14,
            "剑心通明 doubles this combat's maximum to 14, not the kept maximum");
        Hero.GetPower<SwordheartClarityPower>()!.SetAmount(4, silent: true);
        Check(SwordheartPower.GetMaxAmount(Hero) == 14 &&
            SwordheartClarityPower.GetSwordheartConsumptionAmount(Hero, 2m) == 4m,
            "legacy stacked Clarity keeps one capacity/consumption doubling, never four doublings");
        Power<SwordheartPower>(Hero, 12);
        ManualCall("KeepSwordheartAfterCombat");
        Check(FateValue<int>("Swordheart") == 7, "at the end of combat 12 swordheart are cut to the kept seven");
        Reset(); As31O(true);
        Power<SwordheartPower>(Hero, 4);
        ManualCall("KeepSwordheartAfterCombat");
        Check(FateValue<int>("Swordheart") == 4, "four swordheart are kept as they are");
        Reset(); As31O(true);
        ManualCall("KeepSwordheartAfterCombat");
        Check(FateValue<int>("Swordheart") == 0, "a combat that ends with no swordheart keeps none");
        As31O(false);
        Check(SwordheartPower.GetKeptMaximum(Hero) == 0, "other characters keep no swordheart");
    }

    static async Task FateCommandCases()
    {
        Type commandType = Mod.GetType("HBRGOSakiMod.Core.FateDice.HbrFateDiceCommand", true)!;
        Type opType = Mod.GetType("HBRGOSakiMod.Core.FateDice.HbrFateDiceOp", true)!;
        object Command(string op, string fragment, string? receipt = null, string? shop = null)
        {
            object command = Activator.CreateInstance(commandType, true)!;
            commandType.GetProperty("Op", F)!.SetValue(command, Enum.Parse(opType, op));
            commandType.GetProperty("Fragment", F)!.SetValue(command, fragment);
            commandType.GetProperty("Receipt", F)!.SetValue(command, receipt);
            commandType.GetProperty("ShopKey", F)!.SetValue(command, shop);
            return command;
        }
        Task<bool> Apply(object command) => (Task<bool>)ManualCall("ApplyFateDiceCommand", command)!;

        Reset(); As31O(true); CombatInProgress(false);
        Fate(FateJson(bag: new[] { Floor, Mirror }, bosses: 0));
        Check(await Apply(Command("Equip", Floor)) && Equipped.SequenceEqual(new[] { Floor }) && Bag.SequenceEqual(new[] { Mirror }),
            "outside combat the manual fits 托底 from the bag");
        Check(await Apply(Command("Unequip", Floor)) && Equipped.Length == 0 && Bag.SequenceEqual(new[] { Mirror, Floor }),
            "and takes it off again");
        Check(!await Apply(Command("Equip", WideEdge)), "a fragment the player does not own cannot be fitted");
        string saved = FateManual.HbrFateDiceData;
        Check(saved.Contains("\"Bag\":[\"MIRROR\",\"FLOOR\"]", StringComparison.Ordinal), "the record is saved on the manual: " + saved);

        CombatInProgress(true);
        try
        {
            Check(!await Apply(Command("Equip", Mirror)) && Equipped.Length == 0, "during combat the loadout is locked");
        }
        finally { CombatInProgress(false); }

        Fate(FateJson(receipts: new[] { "fate:test" }, bosses: 0));
        Check(await Apply(Command("Claim", Mirror, receipt: "fate:test")) && Equipped.SequenceEqual(new[] { Mirror }) &&
              FateValue<string[]>("Claimed").SequenceEqual(new[] { "fate:test" }),
            "a claimed reward fragment goes into a free slot");
        Check(!await Apply(Command("Claim", Floor, receipt: "fate:test")) && !await Apply(Command("Claim", Floor, receipt: "fate:other")),
            "a reward set is claimed once, and only a rolled one");
        Check(!await Apply(Command("Buy", Floor, shop: "nowhere")) && !FateValue<string[]>("Bag").Contains(Floor),
            "buying outside the merchant's room fails");
    }

    static void FateStructureCases()
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (string hook in new[] { "AfterDamageReceived", "AfterSideTurnEnd", "AfterCardChangedPiles" })
            Check(typeof(MagicSwordModePower).GetMethod(hook, Declared) == null,
                "魔剑模式 no longer overrides " + hook + " (the survival rules and the dual swords exit are gone)");
        Check(typeof(SwordheartPower).GetMethod("AfterPowerAmountChanged", Declared) == null,
            "a full swordheart no longer puts a 魔剑模式 card into the hand");
        Reset(); As31O(true);
        Power<MagicSwordModePower>(Hero, 1);
        Check(!(bool)Call("ModeTransitionHelper", "IsWeaponModeWeakened", Hero)!,
            "the magic sword no longer weakens the bow or dual swords form");
        var retired = (IEnumerable<Type>)Mod.GetType("HBRGOSakiMod.Core.Integration.HBRGOSakiContentPack", true)!
            .GetField("RetiredCardTypes", F)!.GetValue(null)!;
        Check(retired.Contains(typeof(AccelerationHbrgoSaki)) && retired.Contains(typeof(ProtagonistPrivilegeHbrgoSaki)),
            "the 加速 and 主人公特典 cards are retired (they became fate fragments) and stay loadable for old saves");
    }
}
