using System.Buffers.Binary;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

public sealed class XMember0 : CardModel, IThirtyOneXMemberCard { public ThirtyOneXMember ThirtyOneXMember => ThirtyOneXMember.CarolReaper; public XMember0() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { } }
public sealed class XMember1 : CardModel, IThirtyOneXMemberCard { public ThirtyOneXMember ThirtyOneXMember => ThirtyOneXMember.LiYingxia; public XMember1() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { } }
public sealed class XMember2 : CardModel, IThirtyOneXMemberCard { public ThirtyOneXMember ThirtyOneXMember => ThirtyOneXMember.IreneRedmayne; public XMember2() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { } }
public sealed class XMember3 : CardModel, IThirtyOneXMemberCard { public ThirtyOneXMember ThirtyOneXMember => ThirtyOneXMember.VritikaBalakrishnan; public XMember3() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { } }
public sealed class XMember4 : CardModel, IThirtyOneXMemberCard { public ThirtyOneXMember ThirtyOneXMember => ThirtyOneXMember.MariaDeAngelis; public XMember4() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { } }
public sealed class XMember5 : CardModel, IThirtyOneXMemberCard { public ThirtyOneXMember ThirtyOneXMember => ThirtyOneXMember.CharlottaSkopovskaya; public XMember5() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { } }
static class ThirtyOneXStandalone { public static void Run(string path) => EffectChecks.RunThirtyOneX(path); }
static partial class EffectChecks
{
    static readonly Type[] XMembers = [typeof(XMember0), typeof(XMember1), typeof(XMember2), typeof(XMember3), typeof(XMember4), typeof(XMember5)];
    static readonly Type[] XAuthoredMembers = [typeof(ReadySteadyDieHbrgoSaki), typeof(WitnessMySupremacyHbrgoSaki),
        typeof(ThereIsOnlyOneTruthHbrgoSaki), typeof(NumbersAreTheSourceHbrgoSaki), typeof(RepentanceTimeHbrgoSaki), typeof(LoveCharlottaHbrgoSaki)];
    static bool XHpProbe;
    static int XRealCannonCommands;
    static readonly List<decimal> XHpMultipliers = [];
    static uint XMask(Player? owner = null) => (uint)Call("ThirtyOneXBondHelper", "Members", owner ?? Player)!;
    static decimal XPercent(int member, decimal basis, Player? owner = null) => (decimal)Call("ThirtyOneXBondHelper", "MemberPercent", owner ?? Player, (ThirtyOneXMember)member, basis)!;
    static CardModel XOrigin(int member, Player? owner = null)
    { owner ??= Player; var card = FCard(XMembers[member], owner); card.Pile!.RemoveInternal(card); owner.Deck.AddInternal(card); return card; }
    static ThirtyOneXCollection XMarker(Player owner)
    {
        var relic = (ThirtyOneXCollection)ModelDb.Relic<ThirtyOneXCollection>().ToMutable(); Set(relic, "_owner", owner);
        Set(owner, "_relics", owner.Relics.Append(relic).ToList()); return relic;
    }
    static ThirtyOneXCollection XReset(uint mask, bool hbr = true)
    {
        FReset(0, hbr); Set(Hero, "_block", 0); Set(Enemy, "_block", 0); XHpProbe = false; XHpMultipliers.Clear(); XRealCannonCommands = 0;
        var relic = XMarker(Player); for (int i = 0; i < 6; i++) if ((mask & (1u << i)) != 0) XOrigin(i); return relic;
    }
    static bool XListeners(ref IEnumerable<AbstractModel> __result)
    {
        __result = Combat.Creatures.SelectMany(c => c.Powers).Cast<AbstractModel>().Concat(RunState.Players.SelectMany(p => p.Relics))
            .Concat(RunState.Players.SelectMany(p => p.PlayerCombatState?.AllCards ?? Array.Empty<CardModel>())).ToArray(); return false;
    }
    static bool XHealNoAnimation(ref bool playAnim) { playAnim = false; return true; }
    static bool XDamageLeaf(object[] __args, ref Task<IEnumerable<DamageResult>> __result)
    {
        if (XHpProbe)
            XHpMultipliers.Add((decimal)Call("ThirtyOneXBondHelper", "GetDamageMultiplier", Enemy, Hero, ValueProp.Move)!);
        if (AsyncValue("HotaruCannonStatsHelper", "Current") is { } scope && scope.GetType().GetProperty("Cannon", F)!.GetValue(scope) != null)
            XRealCannonCommands++;
        __result = FDamageBody(__args); return false;
    }
    static decimal XDamage(Creature target, Creature? dealer, ValueProp props) => (decimal)Call("ThirtyOneXBondHelper", "GetDamageMultiplier", target, dealer, props)!;
    static async Task XSubsetCases()
    {
        for (uint mask = 0; mask < 64; mask++)
        {
            int kinds = BitOperations.PopCount(mask); if (kinds is not (0 or 1 or 2 or 4 or 6)) continue;
            var relic = XReset(mask); await relic.BeforeCombatStart(); int tier = kinds >= 6 ? 3 : kinds >= 4 ? 2 : kinds >= 2 ? 1 : 0;
            Equal(relic.DisplayAmount, kinds, "87 native permanent distinct member count mask" + mask);
            for (int member = 0; member < 6; member++)
            {
                decimal basis = member is 0 or 2 ? 10m : member is 3 or 5 ? 5m : member == 4 ? 3m : 1m;
                Equal(XPercent(member, basis), (mask & (1u << member)) != 0 ? basis * tier : 0m, "87 arbitrary subset gives only its present member bonus " + mask + "/" + member);
            }
            string description = relic.DynamicDescription.GetFormattedText();
            Check(Enumerable.Range(0, 6).All(i => description.Contains(new LocString("relics", relic.Id.Entry + ".member." + i).GetRawText().Split('：')[0] + "：") == (tier > 0 && (mask & (1u << i)) != 0)), "87 actual relic hover lists exactly this frozen subset's active members " + mask);
        }
        var marker = XReset(3); XOrigin(0); await marker.BeforeCombatStart(); Equal(marker.DisplayAmount, 2, "87 duplicate same-member physical cards count only once");
        XOrigin(2); XOrigin(3); Equal(XMask(), 3, "87 midbattle additions do not replace the frozen member snapshot"); Equal(XPercent(0, 10), 10, "87 tier stays fixed until next combat");
        foreach (CardModel removed in Player.Deck.Cards.Where(c => c is XMember0).ToArray()) Player.Deck.RemoveInternal(removed); Equal(XPercent(0, 10), 10, "87 midbattle deletion does not cancel this battle's member effect");
        await marker.AfterCombatEnd(null!); GNextBattle(); await marker.BeforeCombatStart(); Equal(XMask(), 14, "87 next combat sees both additions and full member deletion");
        marker = XReset(3, false); await marker.BeforeCombatStart(); Equal(XPercent(0, 10), 0, "87 legacy mode never receives new HBR-only bonuses");
    }
    static async Task XAuthoredMemberCases()
    {
        var marker = XReset(0);
        string[] stories = ["carol", "yingxia", "irene", "vritika", "maria", "charlotta"];
        for (int i = 0; i < XAuthoredMembers.Length; i++)
        {
            CardModel card = FCard(XAuthoredMembers[i]); card.Pile!.RemoveInternal(card); Player.Deck.AddInternal(card);
            Check(((IThirtyOneXMemberCard)card).ThirtyOneXMember == (ThirtyOneXMember)i, "89–94 real card maps to correct squad role " + XAuthoredMembers[i].Name);
            Check((string)Call("HbrMemoryGarden", "CharacterIdOf", card)! == stories[i], "89–94 real garden card uses stable story identity " + stories[i]);
            Equal((int)Invoke(Mod.GetType("HBRGOSakiMod.Core.Models.Capabilities.ThirtyOneXKeywordRules", true)!.GetMethod("GetPermanentDistinctCount", F)!, null, Player)!, i + 1, "89–94 actual permanent deck grows by one distinct role " + stories[i]);
        }
        CardModel duplicate = FCard(typeof(ReadySteadyDieHbrgoSaki)); duplicate.Pile!.RemoveInternal(duplicate); Player.Deck.AddInternal(duplicate);
        await marker.BeforeCombatStart();
        Equal(marker.DisplayAmount, 6, "89–94 actual six-card permanent deck and duplicate produce six kinds");
        Equal(XMask(), 63, "89–94 real cards produce complete frozen31X battle member mask");
        for (int member = 0; member < 6; member++)
        {
            decimal basis = member is 0 or 2 ? 10m : member is 3 or 5 ? 5m : member == 4 ? 3m : 1m;
            Equal(XPercent(member, basis), basis * 3m, "89–94 actual cards activate correct highest-tier member bonus " + stories[member]);
        }
        foreach (CardModel card in Player.Deck.Cards.Where(card => card is ThereIsOnlyOneTruthHbrgoSaki).ToArray()) Player.Deck.RemoveInternal(card);
        Equal(XMask(), 63, "89–94 real-card midbattle removal retains battle snapshot");
        await marker.AfterCombatEnd(null!); GNextBattle(); await marker.BeforeCombatStart(); Equal(XMask(), 59, "89–94 next battle sees actual Irene deletion while duplicated Carol still counts once");
    }
    static async Task XDamageCases()
    {
        var relic = XReset(3); await relic.BeforeCombatStart(); Equal(XDamage(Enemy, Hero, ValueProp.Move), 1.1m, "87 Carol boosts HP attacks by10percent");
        Equal(XDamage(Enemy, Hero, ValueProp.Unpowered), 1, "87 direct nonattack effect is excluded"); Equal(XDamage(Hero, Hero, ValueProp.Move), 1, "87 self damage is excluded"); Equal(XDamage(Enemy, null, ValueProp.Move), 1, "87 null-dealer environment is excluded");
        var phantom = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!; Power<PhantomPower>(phantom, 1); Set(Combat, "_allies", new List<Creature> { Hero, phantom });
        Equal(XDamage(Enemy, phantom, ValueProp.Move), 1.1m, "87 owned phantom attacks inherit Carol's member bonus");
        Equal(XDamage(Enemy, Hero, ValueProp.Move), 1.1m, "87 hero-dealer floating cannon route retains the same bonus");
        var shell = Power<HighDimensionalShellPower>(Enemy, 1000); Equal(XDamage(Enemy, Hero, ValueProp.Move), 1, "87 Carol alone does not boost DP attacks");
        XHpProbe = true; await (Task<IEnumerable<DamageResult>>)Call("HpOnlyDamageContext", "Damage", new ThrowingPlayerChoiceContext(), Enemy, 6m, Hero, null)!; XHpProbe = false;
        Check(XHpMultipliers.SequenceEqual([1.1m]), "87 explicit HP-only canonical intent sees Carol despite target retaining DP");
        relic = XReset(6); await relic.BeforeCombatStart(); Power<HighDimensionalShellPower>(Enemy, 1000); Equal(XDamage(Enemy, Hero, ValueProp.Move), 1.1m, "87 Irene boosts only DP attacks");
        Powers(Enemy).Clear(); Equal(XDamage(Enemy, Hero, ValueProp.Move), 1, "87 Irene does not boost exposed HP");
        decimal phantomBaseline = await XActualOwnedAttack(6, false), phantomBoosted = await XActualOwnedAttack(3, false);
        Check(phantomBoosted > phantomBaseline && Math.Abs(phantomBoosted - phantomBaseline * 1.1m) <= 1m, "87 actual owned phantom command enters the shared pipeline's HP bonus");
        decimal cannonBaseline = await XActualOwnedAttack(6, true), cannonBoosted = await XActualOwnedAttack(3, true);
        Check(cannonBoosted > cannonBaseline && Math.Abs(cannonBoosted - cannonBaseline * 1.1m) <= 1m && XRealCannonCommands == 1, "87 actual CannonStats.Damage command enters owner attack pipeline and keeps soul scaling");
        relic = XReset(6); await relic.BeforeCombatStart(); await Declared(6m, Hero); decimal baseline = FDamage.Last().Final;
        relic = XReset(3); await relic.BeforeCombatStart(); await Declared(6m, Hero); decimal withCarol = FDamage.Last().Final;
        Equal(withCarol, baseline, "87 unpowered effect-skill arithmetic does not become an attack solely through normalization");
        await relic.AfterCombatEnd(null!); Equal(XDamage(Enemy, Hero, ValueProp.Move), 1, "87 damage bonus is inactive after battle closure");
    }
    static async Task<decimal> XActualOwnedAttack(uint mask, bool cannon)
    {
        var marker = XReset(mask); await marker.BeforeCombatStart();
        Creature unit = (Creature)H("Actor", cannon ? Blank<HotaruContestCannon>() : Blank<SmallHopper>(), null, Player)!;
        Set(Combat, "_allies", new List<Creature> { Hero, unit });
        if (cannon)
        {
            var millsage = (Millsage)ModelDb.Relic<Millsage>().ToMutable(); Set(millsage, "_owner", Player); Set(Player, "_relics", Player.Relics.Append(millsage).ToList());
            await (Task<bool>)Call("HotaruCannonStatsHelper", "Damage", unit, Enemy, 6m, new ThrowingPlayerChoiceContext(), null, null, 1)!;
        }
        else { Power<PhantomPower>(unit, 1); await Command(unit, 6m, ValueProp.Move, null, new[] { Enemy }); }
        return FDamage.Last().Final;
    }
    static async Task XMaximumCases()
    {
        foreach (uint mask in new[] { 33u, 39u, 63u })
        {
            var relic = XReset(mask); var dp = NewDp(100); int before = dp.MaxDp; await relic.BeforeCombatStart(); int tier = BitOperations.PopCount(mask) / 2;
            Equal(dp.MaxDp, decimal.Floor(before * (1m + .05m * tier)), "87 Charlotta scales actual maximumDP by tier" + tier);
            Equal(dp.Dp, 100 + dp.MaxDp - before, "87 currentDP gets only the maximum's exact increase tier" + tier);
            int now = dp.Dp; await relic.BeforeCombatStart(); Equal(dp.Dp, now, "87 repeated native opening cannot refill twice"); await dp.BeforeCombatStart(); Equal(dp.Dp, now, "87 later Deflector callback cannot refill again");
            await relic.AfterCombatEnd(null!); Equal(dp.MaxDp, before, "87 battle closure restores unscaled maximum"); Check(dp.Dp <= dp.MaxDp, "87 battle closure clamps currentDP to ordinary cap");
        }
        var marker = XReset(33); var fresh = NewDp(0); Set(fresh, "_hasInitializedDp", false); int maximum = fresh.MaxDp;
        await marker.BeforeCombatStart(); Equal(fresh.Dp, decimal.Floor(maximum * 1.05m), "87 collection-first initialization grants a scaled full pool exactly once"); await fresh.BeforeCombatStart(); Equal(fresh.Dp, fresh.MaxDp, "87 subsequent Deflector-first callback is idempotent");
        foreach (bool narara in new[] { false, true })
        {
            marker = XReset(33); Creature phantom = (Creature)H("Actor", narara ? Blank<NararaShibaBossMonster>() : Blank<SmallHopper>(), null, Player)!;
            Set(phantom, "_maxHp", 1000); Set(phantom, "_currentHp", 1000); Power<PhantomPower>(phantom, 1); Set(Combat, "_allies", new List<Creature> { Hero, phantom });
            if (narara) { Set(phantom.Monster!, "<IsMutable>k__BackingField", true); Native(phantom.Monster!, "InitializeDpLayers"); }
            HighDimensionalShellPower shell = narara ? Power<NararaLayeredDpPower>(phantom, 75) : Power<PhantomHighDimensionalShellPower>(phantom, 75);
            decimal old = shell.MaximumDp; Check(old > 1, "87 owned phantom fixture has an initialized meaningful base pool, Narara=" + narara); await marker.BeforeCombatStart(); Equal(shell.MaximumDp, decimal.Floor(old * 1.05m), "87 owned main phantom maximum scales once, Narara=" + narara);
            Equal(shell.CurrentDp, 75 + shell.MaximumDp - old, "87 owned phantom current pool gets the exact cap increase, Narara=" + narara); await marker.AfterCombatEnd(null!); Equal(shell.MaximumDp, old, "87 owned phantom cap reverts after end, Narara=" + narara);
        }
    }
    static decimal XPotency(PowerModel p) => decimal.Parse((string)p.GetType().GetProperty("HbrEffectData", F)!.GetValue(p)!, System.Globalization.CultureInfo.InvariantCulture);
    static async Task XAuraCases()
    {
        var relic = XReset(9); await relic.BeforeCombatStart(); var aura = Enemy.Powers.OfType<HbrAttackDownPower>().Single();
        Equal(XPotency(aura), (decimal)Call("HbrPercentDebuffHelper", "ResolveBasis", ModelDb.Power<HbrAttackDownPower>(), 5m, Hero, Enemy)!, "87 Vritika aura uses the normal attribute-based debuff formula");
        object profile = typeof(HbrPercentDebuffPower).GetProperty("CurrentEffectProfile", F)!.GetValue(aura)!;
        Check(profile.GetType().GetProperty("Limit")!.GetValue(profile)!.ToString() == "None" && profile.GetType().GetProperty("Exit")!.GetValue(profile)!.ToString() == "Eternal", "87 aura is an independent unlimited permanent slot");
        foreach (decimal basis in new[] { 30m, 45m }) await (Task)Helper("HbrPercentDebuffApplication").GetMethod("ApplyBasis", F)!.MakeGenericMethod(typeof(HbrAttackDownPower)).Invoke(null, [new ThrowingPlayerChoiceContext(), Enemy, basis, Hero, null, null, false])!;
        Equal(Enemy.Powers.OfType<HbrAttackDownPower>().Count(), 3, "87 aura does not consume either ordinary two-slot debuff allowance");
        relic = XReset(9); Power<ArtifactPower>(Enemy, 1); await relic.BeforeCombatStart(); Equal(Enemy.GetPowerAmount<ArtifactPower>(), 0, "87 actual Artifact blocks one aura attempt");
        Check(!Enemy.HasPower<HbrAttackDownPower>(), "87 blocked aura creates no hidden effect"); await relic.AfterCreatureAddedToCombat(Enemy); Check(!Enemy.HasPower<HbrAttackDownPower>(), "87 Artifact removal does not trigger a second aura attempt on the same enemy");
        Creature newcomer = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!; Set(Combat, "_enemies", new List<Creature> { Enemy, newcomer }); await relic.AfterCreatureAddedToCombat(newcomer);
        Equal(newcomer.Powers.OfType<HbrAttackDownPower>().Count(), 1, "87 new enemy gets one aura midbattle"); await relic.AfterCreatureAddedToCombat(newcomer); Equal(newcomer.Powers.OfType<HbrAttackDownPower>().Count(), 1, "87 repeated native added callback is idempotent");
        relic = XReset(9); Player peer = SupportTeammate(); Set(peer, "<NetId>k__BackingField", 2UL); Set(RunState, "_players", new List<Player> { Player, peer }); Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature });
        var peerRelic = XMarker(peer); for (int i = 0; i < 6; i++) XOrigin(i, peer); await relic.BeforeCombatStart(); await peerRelic.BeforeCombatStart(); aura = Enemy.Powers.OfType<HbrAttackDownPower>().Single();
        Equal(XPotency(aura), (decimal)Call("HbrPercentDebuffHelper", "ResolveBasis", ModelDb.Power<HbrAttackDownPower>(), 15m, peer.Creature, Enemy)!, "87 cooperative aura selects only the strongest actual member tier");
    }
    static async Task XDrawAndVictoryCases()
    {
        foreach (uint mask in new[] { 18u, 23u, 63u })
        {
            var relic = XReset(mask); await relic.BeforeCombatStart(); int tier = BitOperations.PopCount(mask) / 2;
            for (int i = 0; i < 12; i++) FCard(typeof(FOtherCard), hand: false);
            await FSetupTurn(); Equal(Player.PlayerCombatState!.Hand.Cards.Count, 5 + tier, "87 real native first ordinary SetupPlayerTurn draws extra tier" + tier);
            Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2); Equal(relic.ModifyHandDraw(Player, 5), 5, "87 later ordinary turn has no opening bonus");
            Set(Hero, "_maxHp", 5000); Set(Hero, "_currentHp", 1000); await relic.AfterCombatVictory(null!); Equal(Hero.CurrentHp, 1000 + 150 * tier, "87 actual victory HP recovery is3percent per tier");
            await relic.AfterCombatVictory(null!); Equal(Hero.CurrentHp, 1000 + 150 * tier, "87 repeated victory callback never heals twice");
        }
        var marker = XReset(18); await marker.BeforeCombatStart(); Set(Hero, "_currentHp", 1000); await marker.AfterCombatEnd(null!); Equal(Hero.CurrentHp, 1000, "87 failure or ordinary battle end does not heal");
        marker = XReset(3); await marker.BeforeCombatStart();
        foreach (bool od in new[] { false, true })
        {
            Type helper = Helper(od ? "OverclockImmediateTurnCoordinator" : "PersonalImmediateTurnCoordinator"); FieldInfo current = helper.GetField("_current", F)!;
            object? old = current.GetValue(null), oldCombat = od ? null : helper.GetField("_combat", F)!.GetValue(null);
            Type sessionType = helper.GetNestedType("Session", F)!; object session;
            if (od) { var gauge = (Overclock)ModelDb.Relic<Overclock>().ToMutable(); Set(gauge, "_owner", Player); session = Activator.CreateInstance(sessionType, F, null, new object[] { gauge, Combat, new[] { Player } }, null)!; }
            else { session = Activator.CreateInstance(sessionType, true)!; sessionType.GetField("Owner", F)!.SetValue(session, Player); sessionType.GetField("Combat", F)!.SetValue(session, Combat); helper.GetField("_combat", F)!.SetValue(null, Combat); }
            current.SetValue(null, session);
            try { Check(Call("OverclockImmediateTurnCoordinator", "GetTurnToken", Player) != null, "87 inserted-turn fixture owns an actual live token, OD=" + od); Equal(marker.ModifyHandDraw(Player, 5), 5, "87 extra turn retaining native turn1 never repeats opening draw, OD=" + od); }
            finally { current.SetValue(null, old); if (!od) helper.GetField("_combat", F)!.SetValue(null, oldCombat); }
        }
    }
    static void XMetadataCases()
    {
        Check(Enum.GetValues<ThirtyOneXMember>().Length == 6, "87 six exact31X member identities exist");
        Check(Mod.GetTypes().Where(t => !t.IsAbstract && typeof(CardModel).IsAssignableFrom(t) && typeof(IThirtyOneXMemberCard).IsAssignableFrom(t)).ToHashSet().SetEquals(XAuthoredMembers), "89–94 production contains exactly the six authorized31X member cards, excluding derived choice/energy tokens");
        string[] ids = ["carol", "yingxia", "irene", "vritika", "maria", "charlotta"];
        string source = File.ReadAllText(Path.Combine(RepositoryRoot, "Core/Story/HbrStoryCharacterCatalog.cs")); Check(ids.All(id => source.Contains("\"" + id + "\"")), "87 story catalog has stable IDs " + string.Join(",", ids));
        byte[] bytes = File.ReadAllBytes(Path.Combine(RepositoryRoot, "images/relics/thirty_one_x.png"));
        Check(Convert.ToHexString(SHA256.HashData(bytes)) == "BAF116B0E4F672D280EDF0465C12B8A9E1960404FDCE64DF0DAABBF50572118E", "87 logo retains exact supplied original bytes");
        Check(BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) == 420 && BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) == 420, "87 actual original logo is420x420");
        using var changes = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "outputs/31f-and-31x-83-88-20261115/squad-name-changes88.json")));
        foreach (JsonElement entry in changes.RootElement.EnumerateArray())
        {
            string language = entry.GetProperty("language").GetString()!, key = entry.GetProperty("key").GetString()!;
            var table = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(RepositoryRoot, "HBRGOSakiMod/localization", language, "relics.json")))!;
            Check(table[key] == entry.GetProperty("after").GetString(), "88 intended display-only change is exact " + language + "/" + key);
        }
        foreach (string language in new[] { "zhs", "eng" })
        {
            var table = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(RepositoryRoot, "HBRGOSakiMod/localization", language, "relics.json")))!;
            foreach (string name in new[] { "虎口夺食", "家人就是这样", "魔王与使魔", "CRYCHIC" }) Check(table.Any(p => p.Key.EndsWith(".title") && p.Value == name), "88 keeps non-squad title unchanged " + language + "/" + name);
            Check(table["THIRTY_G_COLLECTION_RELIC.title"] == "30G", "88 actual30G title is only its squad number " + language);
        }
    }
    public static void RunThirtyOneX(string path)
    {
        bool ok = false; string? error = null; ulong? local = LocalContext.NetId;
        try
        {
            Install(); Localization(); LocalContext.NetId = 1UL; var h = new Harmony(ProductionId + ".31x87");
            foreach (Type type in XMembers.Concat(XAuthoredMembers).Concat(FMembers).Concat(new[] { typeof(ThirtyOneXCollection), typeof(ThirtyOneFCollection), typeof(FOtherCard), typeof(HbrAttackDownPower), typeof(ArtifactPower), typeof(PhantomPower), typeof(HighDimensionalShellPower), typeof(PhantomHighDimensionalShellPower), typeof(NararaLayeredDpPower), typeof(NararaShibaBossMonster), typeof(HotaruContestCannon), typeof(Millsage), typeof(Overclock) }))
            { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
            void Patch(MethodBase method, string name) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(name, F)!));
            foreach (Type type in new[] { typeof(RunState), typeof(CombatState) }) h.Patch(type.GetMethod("IterateHookListeners", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(XListeners), F)!) { priority = Priority.First + 200 });
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(FormNotEnding)); Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip)); Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCreatureIntent", F)!, nameof(VisualTask));
            foreach (var method in typeof(Cmd).GetMethods(F).Where(m => m.Name is "Wait" or "CustomScaledWait" && m.ReturnType == typeof(Task))) Patch(method, nameof(VisualTask));
            foreach (var method in typeof(SfxCmd).GetMethods(F).Where(m => m.Name == "Play")) Patch(method, nameof(Skip));
            foreach (string name in new[] { "HotaruCannonDamageContextPatch", "ThirtyOneXDescriptionPatch" }) h.CreateClassProcessor(Mod.GetTypes().Single(t => t.Name == name)).Patch();
            Patch(typeof(CreatureCmd).GetMethod("Heal", F)!, nameof(XHealNoAnimation));
            Patch(typeof(CardPileCmd).GetMethods(F).Single(m => m.Name == "Add" && m.GetParameters() is var p && p.Length == 5 && p[0].ParameterType == typeof(CardModel) && p[1].ParameterType == typeof(CardPile)), nameof(FAddLeaf));
            h.Unpatch(Canonical, typeof(EffectChecks).GetMethod(nameof(Terminal), F)!); h.Patch(Canonical, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(XDamageLeaf), F)!) { priority = Priority.Last });
            try { XMetadataCases(); XSubsetCases().GetAwaiter().GetResult(); XAuthoredMemberCases().GetAwaiter().GetResult(); XMaximumCases().GetAwaiter().GetResult(); XAuraCases().GetAwaiter().GetResult(); XDrawAndVictoryCases().GetAwaiter().GetResult(); XDamageCases().GetAwaiter().GetResult(); ok = true; }
            finally { h.UnpatchAll(h.Id); }
        }
        catch (Exception e) { error = e.ToString(); Console.Error.WriteLine(e); }
        finally { LocalContext.NetId = local; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), profile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Real permanent deck, six authored31X card mappings plus arbitrary test subsets, member/battle snapshot, native power applications/Artifact, DP pools, native SetupPlayerTurn/Draw and HP Heal. Input/visual/wait and canonical damage presentation leaves controlled; no Godot or live network." }, new JsonSerializerOptions { WriteIndented = true }));
        if (!ok) throw new Exception("Inspect " + path); Console.WriteLine("PASS " + Passed.Count + "31X87 checks");
    }
}
