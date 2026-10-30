using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

static partial class Checks
{
    static void FragilePercentCases()
    {
        CardModel thunder = Card(typeof(MimicThunderGaleHbrgoSaki));
        string text = Render(thunder, false);
        Check(text.Contains("给予37.5%的脆弱（2回合）"), "Thunder modifier2 renders37point5percent and fixed2turn duration");
        Check(!text.Contains("2层脆弱"), "Thunder no longer labels its modifier input as actual Fragile stacks");
        Upgrade(thunder);
        Check(Render(thunder, true).Contains("给予37.5%的脆弱（2回合）"), "Thunder upgrade retains raw2 and same Fragile coefficient");

        CardModel ice = Card(typeof(MimicIceFlowerThousandArrowsHbrgoSaki));
        text = Render(ice, false);
        Check(text.Contains("46.9%的脆弱（2回合）"), "Ice modifier3 rounds display46point9percent");
        Check(text.Contains("达到5张时") && text.Contains("额外给予23.4%的防御力下降（2回合）") && text.Contains("脆弱变为91.6%（2回合）"),
            "Ice threshold5 displays one B6 Fragile grant91point6percent and independent raw3 defense-down23point4percent");
        Check(ice.DynamicVars["FragilePower"].BaseValue == 3m && ice.DynamicVars["ThresholdFragilePower"].BaseValue == 3m,
            "Ice formatting keeps both original modifier counters3 unmodified");
        CardModel clone = (CardModel)ice.ClonePreservingMutability(); Upgrade(clone);
        text = Render(clone, false);
        Check(text.Contains("73.2%的脆弱（2回合）") && text.Contains("178.8%（2回合）") && text.Contains("达到3张时"),
            "Ice upgraded raw5 plus4 renders73point2 and178point8percent at threshold3");
        Check(ice.DynamicVars["FragilePower"].BaseValue == 3m && clone.DynamicVars["FragilePower"].BaseValue == 5m,
            "original and upgraded clone retain independent raw Fragile counters");

        CardModel opening = Card(typeof(MagicSwordLuckySevenOpeningHbrgoSaki));
        Call(opening.GetType(), "SetConsumedSwordheartAmount", opening, 6);
        Check(Render(opening, false).Contains("91.6%的脆弱（2回合）"),
            "LuckySevenOpening formats actual consumedSwordheart6 into91point6percent");
        CardModel summer = Card(typeof(SummerGrassDreamHbrgoSaki));
        Set(summer, "<UpgradeEffectOrder>k__BackingField", new[] { 2 });
        Set(summer, "_currentUpgradeLevel", 1);
        Check(Render(summer, false).Contains("给予所有敌人37.5%的脆弱（2回合）"),
            "SummerGrassDream conditional HammerEffect formats fixedraw2 into37point5percent");
        string upgradeDescription = (string)Call(Helper("HbrMemoryGardenUpgradeCatalog"),
            "GetEffectDescription", null, summer, "2")!;
        Check(Clean(upgradeDescription).Contains("37.5%的脆弱（2回合）"),
            "memory-garden upgrade catalog uses actual Fragile percentage localization");

        foreach ((Type type, string expected) in new[] {
            (typeof(DoubleArmorBreakHbrgoSaki), "46.9%"), (typeof(FragileHbrgoSaki), "37.5%"),
            (typeof(SoundOfSilenceHbrgoSaki), "73.2%"), (typeof(HotaruDetonation), "30%"),
            (typeof(HotaruDawnGap), "37.5%"), (typeof(HotaruVoiceFromLight), "30%"),
            (typeof(HelloWorldHbrgoSaki), "46.9%"), (typeof(FatePointsHbrgoSaki), "73.2%") })
        {
            text = Render(Card(type), false);
            Check(text.Contains(expected) && text.Contains("（2回合）"), type.Name + " actual HBR template formats percentage" + expected + " and2turn duration");
        }
        CardModel doubling = Card(typeof(DoublePowersHbrgoSaki));
        text = Render(doubling, false);
        Check(text.Contains("每份攻击力下降、防御力下降与脆弱效果数值及剩余持续时间翻倍") && !text.Contains("复制一份"),
            "DoublePowers HBR card explains effect and remaining duration doubling without copying");
        Upgrade(doubling);
        Check(Render(doubling, false).Contains("每份攻击力下降、防御力下降与脆弱效果数值及剩余持续时间翻倍"),
            "upgraded DoublePowers retains effect and duration behavior");
        Check(Render(Card(typeof(HotaruReplayPain)), false).Contains("按原效果各复制一份（2回合）"),
            "ReplayPain retains independent snapshot copying");
        Check(Render(Card(typeof(FragileDoubleStrikeHbrgoSaki)), false).Contains("效果数值翻倍，持续时间不变"),
            "FragileDoubleStrike explains in-place effect doubling without duration refresh");
        foreach (Type type in new[] { typeof(PiercingShotHbrgoSaki), typeof(StrongShotHbrgoSaki) })
        {
            CardModel attack = Card(type);
            text = Render(attack, false);
            Check(text.Contains("基础伤害倍率增加6‰") && text.Contains("1%"),
                type.Name + " explains6permille perFragilepercentagepoint");
            Upgrade(attack);
            Check(Render(attack, false).Contains("基础伤害倍率增加12‰"),
                type.Name + " retains doubled12permille upgraded Fragile bonus");
        }
        CardModel unfading = Card(typeof(HotaruUnfadingScars));
        Check(!Render(unfading, false).Contains("乘算"), "unupgraded UnfadingScars has no effect amplification");
        Upgrade(unfading);
        text = Render(unfading, false);
        Check(text.Contains("最终效果各提高25%（乘算）") && !text.Contains("个百分点"),
            "upgraded UnfadingScars describes final25percent multiplicative enhancement");
        CardModel fullBody = Card(typeof(MimicFullBodyShotHbrgoSaki));
        text = Render(fullBody, false);
        Check(text.Contains("每满10%脆弱") && text.Contains("额外给予1%破坏率") &&
            text.Contains("脆弱百分比数值×100×破坏率"),
            "FullBody describes percentage threshold and confirmed hundred-point damage scale");
        Upgrade(fullBody);
        Check(Render(fullBody, false).Contains("每满5%脆弱"),
            "upgraded FullBody converts every5percentagepoints into1percent destruction");
        Mode(false);
        Check(Render(ice, false).Contains("3层脆弱") && !Render(ice, false).Contains("91.6%"),
            "disabled HBR mode preserves original layer-based Ice text");
        Mode(true);
    }

    static void FragileTargetCases()
    {
        CardModel direct = Card(typeof(DoubleArmorBreakHbrgoSaki));
        typeof(ElectronicSoldierManual).GetProperty("Talent", F)!.SetValue(Manual, 100);
        typeof(ElectronicSoldierManual).GetProperty("Simplicity", F)!.SetValue(Manual, 100);
        Check(Render(direct, false).Contains("46.9%的脆弱"), "without selected target current stats never invent enemyE or inflate base coefficient");
        Check(Render(direct, false, target: Enemy).Contains("53.9%的脆弱"),
            "selected E150 target resolves real INT300 LUCK300 reference300 difference150 into53point9percent");
        using ((IDisposable)Helper("HbrCardDescriptionContext").GetMethods(F)
            .Single(m => m.Name == "Begin" && m.GetParameters().Length == 5)
            .Invoke(null, [direct, false, false, Enemy, true])!)
            Check(Clean(Bind(direct).GetFormattedText()).Contains("46.9%的脆弱"),
                "force-unpowered selected target shows base46point9 instead of live attribute scaling");
        Check(Render(direct, false).Contains("46.9%的脆弱"), "target scope disposal restores exact base Fragile coefficient");
        typeof(ElectronicSoldierManual).GetProperty("Talent", F)!.SetValue(Manual, 0);
        typeof(ElectronicSoldierManual).GetProperty("Simplicity", F)!.SetValue(Manual, 0);

        // Mode switching halves the real raw input before coefficient conversion.
        CardModel thunder = Card(typeof(MimicThunderGaleHbrgoSaki));
        Check(Render(thunder, false, target: Enemy).Contains("30%的脆弱"),
            "off-form Thunder selected target mirrors real raw2 halved to1 before B calculation");
        var dual = (PowerModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ReptureDualSwordsPower));
        Set(dual, "<IsMutable>k__BackingField", true); Set(dual, "_owner", Hero); Set(dual, "_amount", 1);
        ((List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(Hero)!).Add(dual);
        Check(Render(thunder, false, target: Enemy).Contains("37.5%的脆弱"),
            "matching form Thunder selected target retains raw2 and37point5percent");
        ((List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(Hero)!).Remove(dual);

        CardModel odd = Card(typeof(MimicExorcismArrowHbrgoSaki));
        Check(Render(odd, false, target: Enemy).Contains("30%的脆弱"),
            "off-form raw3 becomes1point5 and native integer normalization selects B1 rather than fractional exponent");
        var scars = Blank<HotaruUnfadingScarsPower>();
        Set(scars, "<IsMutable>k__BackingField", true); Set(scars, "_owner", Hero); Set(scars, "_amount", 1);
        typeof(HotaruUnfadingScarsPower).GetProperty("EffectPercent", F)!.SetValue(scars, 25);
        var powers = (List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(Hero)!;
        powers.Add(scars);
        Check(Render(direct, false, target: Enemy).Contains("58.6%的脆弱"),
            "selected-target Scars multiplies resolved46point875 effect by1point25 exactly once");
        typeof(ElectronicSoldierManual).GetProperty("Talent", F)!.SetValue(Manual, 100);
        typeof(ElectronicSoldierManual).GetProperty("Simplicity", F)!.SetValue(Manual, 100);
        Check(Render(direct, false, target: Enemy).Contains("67.4%的脆弱"),
            "Scars multiplies final target attribute result53point90625 by1point25");
        typeof(ElectronicSoldierManual).GetProperty("Talent", F)!.SetValue(Manual, 0);
        typeof(ElectronicSoldierManual).GetProperty("Simplicity", F)!.SetValue(Manual, 0);
        thunder.DynamicVars["FragilePower"].UpgradeValueBy(-1m);
        Check(Render(thunder, false, target: Enemy).Contains("0%的脆弱"),
            "raw1 halved to0point5 creates no Fragile grant and therefore no Scars bonus");
        powers.Remove(scars);
    }

    static readonly List<(Type Power, decimal Amount)> AppliedPowers = [];
    static bool NoCombatEnd(ref bool __result) { __result = false; return false; }
    static bool SkipAttack(AttackCommand __instance, ref Task<AttackCommand> __result)
    { __result = Task.FromResult(__instance); return false; }
    static bool CapturePower(PowerModel power, decimal amount, ref Task __result)
    { AppliedPowers.Add((power.GetType(), amount)); __result = Task.CompletedTask; return false; }

    static async Task IceRealPlayCases()
    {
        var harmony = new Harmony("hbr.fragile.cards.real.ice.play");
        harmony.Patch(AccessTools.Method(typeof(CombatState), "IterateHookListeners"),
            prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(EmptyListeners), F)!));
        harmony.Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"),
            prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(NoCombatEnd), F)!));
        harmony.Patch(AccessTools.Method(typeof(AttackCommand), "Execute"),
            prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(SkipAttack), F)!));
        MethodInfo apply = typeof(PowerCmd).GetMethods(F).Single(m => m.Name == "Apply" && !m.IsGenericMethod &&
            m.GetParameters().Length == 7 && m.GetParameters()[1].ParameterType == typeof(PowerModel));
        harmony.Patch(apply, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(CapturePower), F)!));
        var content = (Dictionary<ModelId, AbstractModel>)typeof(ModelDb).GetField("_contentById", F)!.GetValue(null)!;
        foreach (Type type in new[] { typeof(FragilePower), typeof(HbrDefenseDownPower), typeof(HbrAttackDownPower),
            typeof(MegaCrit.Sts2.Core.Models.Powers.VulnerablePower), typeof(MegaCrit.Sts2.Core.Models.Powers.WeakPower) })
        {
            if (content.Values.Any(model => model.GetType() == type)) continue;
            AbstractModel canonical = (AbstractModel)Activator.CreateInstance(type)!; content[canonical.Id] = canonical;
        }
        var bow = Blank<ReptureBowPower>();
        Set(bow, "<IsMutable>k__BackingField", true); Set(bow, "_owner", Hero); Set(bow, "_amount", 1);
        var powers = (List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(Hero)!;
        powers.Add(bow);
        try
        {
            foreach ((bool enabled, bool upgraded, int bowCount, decimal[] expected) in new[] {
                (true, false, 4, new[] { 3m }), (true, false, 5, new[] { 6m }),
                (true, true, 3, new[] { 9m }), (false, false, 5, new[] { 3m, 3m }) })
            {
                Mode(enabled); AppliedPowers.Clear();
                var deck = new CardPile(PileType.Deck);
                Set(Player, "<Deck>k__BackingField", deck);
                var cards = (List<CardModel>)Field(typeof(CardPile), "_cards").GetValue(deck)!;
                for (int i = 0; i < bowCount; i++) cards.Add(Card(typeof(MimicExorcismArrowHbrgoSaki)));
                CardModel ice = Card(typeof(MimicIceFlowerThousandArrowsHbrgoSaki));
                ice.UpgradePreviewType = CardUpgradePreviewType.Combat;
                if (upgraded) Upgrade(ice);
                var play = Blank<CardPlay>(); Set(play, "<Card>k__BackingField", ice);
                await (Task)Call(ice.GetType(), "OnPlay", ice, new ThrowingPlayerChoiceContext(), play)!;
                decimal[] fragile = AppliedPowers.Where(p => p.Power == typeof(FragilePower)).Select(p => p.Amount).ToArray();
                Check(fragile.SequenceEqual(expected), $"actual Ice OnPlay HBR={enabled} upgraded={upgraded} bows={bowCount} grants raw[{string.Join(',', expected)}] once per intended grant");
                Type defenseType = enabled ? typeof(HbrDefenseDownPower) : typeof(MegaCrit.Sts2.Core.Models.Powers.VulnerablePower);
                Check(AppliedPowers.Count(p => p.Power == defenseType) ==
                    (bowCount >= (upgraded ? 3 : 5) ? 2 : 1), "actual Ice OnPlay preserves separate base and threshold grants with the mode-appropriate defense-down model");
                Check(AppliedPowers.All(p => p.Power != (enabled ? typeof(MegaCrit.Sts2.Core.Models.Powers.VulnerablePower) : typeof(HbrDefenseDownPower))),
                    "actual Ice OnPlay keeps independent HBR defense down separate from native Vulnerable");
            }
        }
        finally { Mode(true); powers.Remove(bow); harmony.UnpatchAll(harmony.Id); }
    }

    public static async Task RunFragileCards()
    {
        bool ok = false; string? error = null;
        try
        {
            Install(); Localization(); World();
            // ModelDb is fully populated in-game; this minimal managed world
            // provides both independent HBR and unchanged native definitions.
            var content = (Dictionary<ModelId, AbstractModel>)typeof(ModelDb).GetField("_contentById", F)!.GetValue(null)!;
            foreach (Type type in new[] { typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower),
                typeof(MegaCrit.Sts2.Core.Models.Powers.WeakPower), typeof(MegaCrit.Sts2.Core.Models.Powers.VulnerablePower) })
            {
                if (content.Values.Any(model => model.GetType() == type)) continue;
                AbstractModel canonical = (AbstractModel)Activator.CreateInstance(type)!;
                content[canonical.Id] = canonical;
            }
            FragilePercentCases(); FragileTargetCases(); await IceRealPlayCases(); ok = true;
        }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, rows = Rows, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            localizationSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,
                "HBRGOSakiMod/localization/zhs/cards.json")))),
            limits = "Reuses real card-display fixture setup; runs actual card models, upgrade and clone routines, target context, production Fragile helper and native LocManager/SmartFormat. Actual Ice OnPlay runs with isolated AttackCommand.Execute and non-generic PowerCmd.Apply leaf capture, recording raw requests before production Fragile lifecycle. Godot nodes and runtime UI are not constructed. No in-game combat, packaging or Steam upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {Passed.Count} Fragile card display checks");
        if (!ok) throw new Exception("Fragile card display checks failed; inspect report.json");
    }
}
