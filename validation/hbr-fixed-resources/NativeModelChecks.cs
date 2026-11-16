using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

static partial class Checks
{
    // Exact v0.111.0 text read from installed SlayTheSpire2.pck (localization/eng and zhs,
    // sts2.dll 0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9).
    static readonly Dictionary<string, string> RelicText = new()
    {
        ["PRECARIOUS_SHEARS.description"] = "Upon pickup, remove [blue]{Cards}[/blue] {Cards:plural:card|cards} from your [gold]Deck[/gold] and lose [blue]{Damage}[/blue] HP.",
        ["ROYAL_POISON.description"] = "At the start of each combat, lose [blue]{Damage}[/blue] HP.",
        ["TUNGSTEN_ROD.description"] = "Whenever you would lose HP, lose [blue]{HpLossReduction}[/blue] less.",
        ["BEATING_REMNANT.description"] = "You cannot lose more than [blue]20[/blue] HP in a single turn.",
        ["SERE_TALON.description"] = "Upon pickup, lose [blue]9[/blue] Max HP. Add [blue]{Wishes}[/blue] [purple]Wishes[/purple] to your [gold]Deck[/gold].",
        ["FAKE_LEES_WAFFLE.description"] = "Upon pickup, heal [green]{Heal}%[/green] of your HP."
    };
    static readonly Dictionary<string, string> CardText = new()
    {
        ["REGRET.description"] = "At the end of your turn, if this is in your [gold]Hand[/gold], lose 1 HP for each card in your [gold]Hand[/gold].",
        ["CRIMSON_MANTLE.description"] = "At the start of your turn, lose 1 HP and gain {CrimsonMantlePower:diff()} [gold]Block[/gold].",
        ["INFERNO.description"] = "At the start of your turn, lose 1 HP.\nWhenever you lose HP on your turn, deal {InfernoPower:diff()} damage to ALL enemies."
    };
    const string RegretChinese = "在你的回合结束时，如果这张牌在你的[gold]手牌[/gold]中，失去相当于[gold]手牌[/gold]数量的生命。";
    static readonly Dictionary<string, string> EnchantmentText = new()
    {
        ["CORRUPTED.description"] = "Deal [blue]50%[/blue] more damage, but lose [blue]2[/blue] HP.",
        ["CORRUPTED.extraCardText"] = "Lose 2 HP."
    };
    static readonly Dictionary<string, string> SelfDamagePowerText = new()
    {
        ["CRIMSON_MANTLE_POWER.smartDescription"] = "At the start of your turn, lose [blue]{SelfDamage}[/blue] HP and gain [blue]{Amount}[/blue] [gold]Block[/gold].",
        ["INFERNO_POWER.smartDescription"] = "At the start of your turn, lose [blue]{SelfDamage}[/blue] HP. Whenever you lose HP on your turn, deal [blue]{Amount}[/blue] damage to ALL enemies."
    };
    // Stable keeps the flag on CombatManager; v0.111.0 moved it to the manager's CombatTurnState.
    static void CombatInProgress(bool value)
    {
        FieldInfo? turnState = typeof(CombatManager).GetField("_turnState", F);
        if (turnState == null) { Set(CombatManager.Instance, "<IsInProgress>k__BackingField", value); return; }
        object state = turnState.GetValue(CombatManager.Instance) ?? RuntimeHelpers.GetUninitializedObject(turnState.FieldType);
        Set(state, "<IsInProgress>k__BackingField", value);
        turnState.SetValue(CombatManager.Instance, value ? state : null);
    }
    static T Named<T>(T model, string category, string entry) where T : AbstractModel
    { Set(model, "<Id>k__BackingField", new ModelId(category, entry)); return model; }
    static string Text(LocString text) => text.GetFormattedText();
    static Dictionary<string, string> ModTable(string language, string table)
    {
        // The shipped mod localization, found from the fixture's output or HBR_REPOSITORY_ROOT.
        string? root = Environment.GetEnvironmentVariable("HBR_REPOSITORY_ROOT");
        for (string? cursor = AppContext.BaseDirectory; root == null && cursor != null; cursor = Path.GetDirectoryName(cursor))
            if (File.Exists(Path.Combine(cursor, "HBRGOSakiMod", "localization", language, table + ".json"))) root = cursor;
        if (root == null) throw new FileNotFoundException("mod localization " + language + "/" + table);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(root, "HBRGOSakiMod", "localization", language, table + ".json")))!;
    }

    // Native relic, card, enchantment and power HP losses found by the 2026-11-05 audit of
    // every HP command and HP-loss hook outside events, on stable and v0.111.0.
    static async Task NativeModelChecks(Harmony h)
    {
        var tables = (Dictionary<string, LocTable>)Field(typeof(LocManager), "_tables").GetValue(LocManager.Instance)!;
        tables["relics"] = new LocTable("relics", RelicText, null!);
        tables["cards"] = new LocTable("cards", CardText, null!);
        tables["enchantments"] = new LocTable("enchantments", EnchantmentText, null!);
        tables["static_hover_tips"] = new LocTable("static_hover_tips", new Dictionary<string, string>(), null!);
        foreach ((string key, string value) in SelfDamagePowerText) PowerText[key] = value;
        foreach ((string name, int bodies) in new[] { ("HbrNativeFixedHpLossCommandPatch", 2), ("HbrNativeFixedLiteralDescriptionPatch", 5) })
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch().Count == bodies,
                $"actual production Harmony patch installs {name} on {bodies} native methods");
        // A FromDeckForRemoval prefix did not run (apparently inlined); an empty deck takes its native no-selection return.
        Set(Player, "<Deck>k__BackingField", new CardPile(PileType.Deck));
        // Prefixes on small native commands did not run inside the patched bodies (apparently
        // inlined). Native non-interactive mode skips Inferno's wait; with no combat in progress,
        // Crimson Mantle's block command returns before its hooks.
        Func<bool> interactive = NonInteractiveMode.AutoSlayerCheck;
        NonInteractiveMode.AutoSlayerCheck = () => true;
        Patch(h, AccessTools.Method(typeof(CardModel), nameof(CardModel.UpdateDynamicVarPreview)), nameof(Skip));
        Set(Player, "<PlayerCombatState>k__BackingField", Blank<PlayerCombatState>());

        // Precarious Shears and Royal Poison lose HP through a DamageVar with no attacker.
        var shears = Named(Relic<PrecariousShears>(), "RELIC", "PRECARIOUS_SHEARS");
        DynamicVar shearsLoss = shears.DynamicVars.Damage;
        Check(shearsLoss.BaseValue == 16 && Read(shearsLoss) == 1600 && shearsLoss.ToString() == "1600" &&
              Read(shears.DynamicVars.Cards) == 2, "Precarious Shears stores native 16, loses 1600 and still removes 2 cards");
        Check(Text(shears.DynamicDescription).Contains("lose [blue]1600[/blue] HP") &&
              Text(shears.DynamicDescription).Contains("remove [blue]2[/blue] cards"), "Precarious Shears text shows 1600 HP and 2 cards");
        await Effect(shears, "AfterObtained");
        Check(Damage[^1] == 1600 && shearsLoss.BaseValue == 16, "real Precarious Shears pickup loses 1600 HP through the typed adapter");
        var poison = Named(Relic<RoyalPoison>(), "RELIC", "ROYAL_POISON");
        Check(Read(poison.DynamicVars.Damage) == 400 && Text(poison.DynamicDescription).Contains("lose [blue]400[/blue] HP"),
            "Royal Poison text shows 400 HP");
        await poison.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player);
        Check(Damage[^1] == 400 && poison.DynamicVars.Damage.BaseValue == 4, "real first-turn Royal Poison loses 400 HP");

        // Tungsten Rod and Beating Remnant change an HP loss by a fixed amount.
        var rod = Named(Relic<TungstenRod>(), "RELIC", "TUNGSTEN_ROD");
        Check(rod.ModifyHpLostAfterOsty(Hero, 500m, ValueProp.Move, null, null) == 400 &&
              rod.ModifyHpLostAfterOsty(Hero, 60m, ValueProp.Move, null, null) == 0 &&
              rod.ModifyHpLostAfterOsty(Blank<Creature>(), 500m, ValueProp.Move, null, null) == 500,
            "Tungsten Rod reduces its owner's HP loss by 100, never below 0, and ignores others");
        Check(Text(rod.DynamicDescription).Contains("lose [blue]100[/blue] less"), "Tungsten Rod text shows 100");
        var remnant = Named(Relic<BeatingRemnant>(), "RELIC", "BEATING_REMNANT");
        CombatInProgress(true);
        Set(remnant, "_damageReceivedThisTurn", 500m);
        Check(Read(remnant.DynamicVars["MaxHpLoss"]) == 2000 && remnant.ModifyHpLostAfterOsty(Hero, 3000m, ValueProp.Move, null, null) == 1500,
            "Beating Remnant caps a turn's HP loss at 2000, after 500 already lost");
        CombatInProgress(false);
        Check(Text(remnant.DynamicDescription).Contains("more than [blue]2000[/blue] HP"), "Beating Remnant literal 20 text shows 2000");

        // v0.111.0 moved the 9 max HP cost from Distinguished Cape to Sere Talon.
        var talon = Named(Relic<SereTalon>(), "RELIC", "SERE_TALON");
        bool talonCost = talon.DynamicVars.ContainsKey("HpLoss");
        Check(talonCost != Relic<DistinguishedCape>().DynamicVars.ContainsKey("HpLoss"),
            "exactly one of Distinguished Cape and Sere Talon carries the 9 max HP cost on this runtime");
        if (talonCost)
        {
            StopAfterCommand = true;
            try { await Effect(talon, "AfterObtained"); throw new Exception("Sere Talon did not reach its isolated card leaf"); }
            catch (StopAtEventUi) { }
            finally { StopAfterCommand = false; }
            Check(Maximum[^1] == -900 && Read(talon.DynamicVars.HpLoss) == 900, "real v0.111.0 Sere Talon pickup loses 900 max HP");
        }
        Check(Text(talon.DynamicDescription).Contains("lose [blue]900[/blue] Max HP"), "v0.111.0 Sere Talon literal 9 text shows 900");

        // Fake Lee's Waffle heals a percentage; it is not a fixed resource.
        var waffle = Named(Relic<FakeLeesWaffle>(), "RELIC", "FAKE_LEES_WAFFLE");
        Check(Read(waffle.DynamicVars.Heal) == 10 && Text(waffle.DynamicDescription).Contains("heal [green]10%[/green]"),
            "Fake Lee's Waffle keeps its 10% heal text");
        await Effect(waffle, "AfterObtained");
        Check(Heals[^1] == 800, "real Fake Lee's Waffle heals 10% of 8000 max HP, not a full heal");

        // Corrupted's literal 2 and Regret's 1 per card reach the decimal command directly.
        var corrupted = Named(Blank<Corrupted>(), "ENCHANTMENT", "CORRUPTED");
        Set(corrupted, "<IsMutable>k__BackingField", true); Set(corrupted, "_card", Card<StrikeIronclad>());
        await Effect(corrupted, "OnPlay", new ThrowingPlayerChoiceContext(), null);
        Check(Damage[^1] == 200, "real Corrupted play loses 200 HP");
        Check(Text(corrupted.DynamicDescription).Contains("lose [blue]200[/blue] HP") &&
              Text(corrupted.DynamicDescription).Contains("[blue]50%[/blue]") && Text(corrupted.DynamicExtraCardText!) == "Lose 200 HP.",
            "Corrupted description and card text show 200 HP and keep 50%");
        var regret = Named(Card<Regret>(), "CARD", "REGRET");
        Set(regret, "_cardsInHand", 5);
        await Effect(regret, "OnTurnEndInHand", new ThrowingPlayerChoiceContext());
        Check(Damage[^1] == 500 && (int)Field(typeof(Regret), "_cardsInHand").GetValue(regret)! == 0,
            "real Regret with 5 cards in hand loses 500 HP and resets its count");
        Check(Text(regret.Description).Contains("lose 100 HP for each card"), "English Regret text shows 100 HP per card");

        // Crimson Mantle and Inferno lose one raw HP per copy played at each turn start.
        var mantle = Power<CrimsonMantlePower>(5);
        mantle.IncrementSelfDamage();
        DynamicVar mantleLoss = mantle.DynamicVars["SelfDamage"];
        Check(mantleLoss.BaseValue == 1 && Read(mantleLoss) == 100, "Crimson Mantle stores raw self-damage 1 and loses 100");
        await mantle.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player);
        Check(Damage[^1] == 100, "real Crimson Mantle turn start loses 100 HP");
        mantle.IncrementSelfDamage();
        Check(mantleLoss.BaseValue == 2 && Read(mantleLoss) == 200, "a second copy increments the raw self-damage, not the scaled one");
        var mantleText = new LocString("powers", "CRIMSON_MANTLE_POWER.smartDescription");
        mantle.DynamicVars.AddTo(mantleText); mantleText.Add("Amount", mantle.Amount);
        Check(Text(mantleText).Contains("lose [blue]200[/blue] HP and gain [blue]5[/blue]"), "Crimson Mantle power text shows 200 HP and 5 Block");
        var inferno = Power<InfernoPower>(4);
        inferno.IncrementSelfDamage();
        await inferno.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player);
        Check(Damage[^1] == 100 && inferno.Amount == 4, "real Inferno turn start loses 100 HP; its 4 damage stacks stay");
        var infernoText = new LocString("powers", "INFERNO_POWER.smartDescription");
        inferno.DynamicVars.AddTo(infernoText); infernoText.Add("Amount", inferno.Amount);
        Check(Text(infernoText).Contains("lose [blue]100[/blue] HP") && Text(infernoText).Contains("deal [blue]4[/blue] damage"),
            "Inferno power text shows 100 HP and keeps 4 damage");
        foreach (CardModel card in new CardModel[] { Named(Card<CrimsonMantle>(), "CARD", "CRIMSON_MANTLE"), Named(Card<Inferno>(), "CARD", "INFERNO") })
        {
            LocString text = card.Description;
            Check(text.GetRawText().Contains("lose {HbrFixedHp} HP") && (decimal)text.Variables["HbrFixedHp"] == 100,
                card.GetType().Name + " card literal 1 HP text shows 100");
        }

        // Disabled HBR mode keeps every native amount and text.
        Mode(false);
        await Effect(shears, "AfterObtained"); Check(Damage[^1] == 16, "disabled Precarious Shears loses native 16");
        await poison.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player); Check(Damage[^1] == 4, "disabled Royal Poison loses native 4");
        Set(remnant, "_damageReceivedThisTurn", 5m);
        CombatInProgress(true);
        Check(rod.ModifyHpLostAfterOsty(Hero, 5m, ValueProp.Move, null, null) == 4 &&
              remnant.ModifyHpLostAfterOsty(Hero, 30m, ValueProp.Move, null, null) == 15, "disabled Tungsten Rod and Beating Remnant keep 1 and 20");
        CombatInProgress(false);
        await Effect(corrupted, "OnPlay", new ThrowingPlayerChoiceContext(), null); Check(Damage[^1] == 2, "disabled Corrupted loses native 2");
        Set(regret, "_cardsInHand", 5);
        await Effect(regret, "OnTurnEndInHand", new ThrowingPlayerChoiceContext()); Check(Damage[^1] == 5, "disabled Regret loses native 5");
        await mantle.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player); Check(Damage[^1] == 2, "disabled Crimson Mantle loses native 2");
        if (talonCost)
        {
            StopAfterCommand = true;
            try { await Effect(talon, "AfterObtained"); } catch (StopAtEventUi) { } finally { StopAfterCommand = false; }
            Check(Maximum[^1] == -9, "disabled v0.111.0 Sere Talon loses native 9 max HP");
        }
        Check(Text(shears.DynamicDescription).Contains("lose [blue]16[/blue] HP") && Text(rod.DynamicDescription).Contains("lose [blue]1[/blue] less") &&
              Text(remnant.DynamicDescription).Contains("[blue]20[/blue]") && Text(talon.DynamicDescription).Contains("[blue]9[/blue]") &&
              Text(corrupted.DynamicExtraCardText!) == "Lose 2 HP." && Text(regret.Description).Contains("lose 1 HP for each card") &&
              Named(Card<Inferno>(), "CARD", "INFERNO").Description.GetRawText() == CardText["INFERNO.description"],
            "disabled HBR mode keeps every native HP text");
        Mode(true);

        // Simplified Chinese words Regret's loss without a number; the mod supplies the HBR text,
        // and only from the current language's own table.
        Dictionary<string, string> modTips = ModTable("zhs", "static_hover_tips");
        tables["cards"] = new LocTable("cards", new Dictionary<string, string> { ["REGRET.description"] = RegretChinese }, null!);
        tables["static_hover_tips"] = new LocTable("static_hover_tips", new Dictionary<string, string>(),
            new LocTable("static_hover_tips", modTips, null!));
        Check(Text(regret.Description) == RegretChinese, "a fallback-table Regret text is not used for the current language");
        tables["static_hover_tips"] = new LocTable("static_hover_tips", modTips, null!);
        Check(Text(regret.Description) == "在你的回合结束时，如果这张牌在你的[gold]手牌[/gold]中，每有一张[gold]手牌[/gold]，失去100点生命。",
            "Simplified Chinese Regret text states 100 HP per card from the shipped mod table");
        Mode(false);
        Check(Text(regret.Description) == RegretChinese, "disabled HBR mode keeps the native Chinese Regret text");
        Mode(true);
        NonInteractiveMode.AutoSlayerCheck = interactive;
    }
}
