using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
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
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock = HBRGOSakiMod.Core.Models.Relics.Overclock;

public sealed class XPlayObserver89 : PowerModel
{
    public int Replays;
    private List<int>? _paid;
    private List<CardModel>? _plays;
    public List<int> Paid => _paid ??= [];
    public List<CardModel> Plays => _plays ??= [];
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int ModifyCardPlayCount(CardModel card, Creature target, int amount) => amount + Replays;
    public override Task AfterEnergySpent(CardModel card, int amount) { Paid.Add(amount); return Task.CompletedTask; }
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play) { Plays.Add(play.Card); return Task.CompletedTask; }
}
public class XDispelBuffA : PowerModel
{
    public static bool Recreate, ThrowAfter;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (Recreate) await PowerCmd.Apply<XDispelBuffA>(new ThrowingPlayerChoiceContext(), oldOwner, 1m, oldOwner, null);
        if (ThrowAfter) throw new InvalidOperationException("controlled dispel callback fault");
    }
}
public sealed class XDispelBuffB : PowerModel { public override PowerType Type => PowerType.Buff; public override PowerStackType StackType => PowerStackType.Counter; }
public sealed class XDispelBuffC : PowerModel { public override PowerType Type => PowerType.Buff; public override PowerStackType StackType => PowerStackType.Counter; }
static partial class EffectChecks
{
    sealed record XCardAttack(CardModel Card, decimal Raw, int Hits, Creature[] Targets, bool Eye, decimal Attack, decimal CritRate, decimal CritDamage, long Qed);
    sealed record XCardHit(CardModel Card, Creature Target, decimal Raw, decimal Final, decimal Amp, decimal Resource, decimal Destruction, bool Forced, int Depth);
    static readonly List<XCardAttack> XCardAttacks = [];
    static readonly List<XCardHit> XCardHits = [];
    static int XCardDepth;
    static bool XCardThrow, XCardNested, XDrainDp, XRecreateArtifact;
    static ExecutionContext? XCardCaptured;
    static void XCardReset(bool hbr = true)
    {
        FReset(0, hbr); Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 1); Set(Player.PlayerCombatState!, "_energy", 20);
        Set(Hero, "_block", 0); Set(Enemy, "_block", 0); Set(Enemy, "_currentHp", 100000000); Set(Enemy, "_maxHp", 100000000);
        XCardAttacks.Clear(); XCardHits.Clear(); XCardDepth = 0; XCardThrow = XCardNested = XDrainDp = XRecreateArtifact = false; XCardCaptured = null;
        XDispelBuffA.Recreate = XDispelBuffA.ThrowAfter = false; Power<XPlayObserver89>(Hero, 1);
    }
    static T XGarden<T>(params int[] upgrades) where T : ThirtyOneBGardenCard
    {
        var card = (T)FCard(typeof(T));
        foreach (int id in upgrades) { Check((bool)Native(card, "PrepareUpgradeEffect", id)!, typeof(T).Name + " accepts upgrade " + id); card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        return card;
    }
    static decimal XPotency<T>(Creature? creature = null) where T : PowerModel
    {
        PowerModel? power = (creature ?? Hero).GetPower<T>();
        return power == null ? 0m : decimal.Parse((string)power.GetType().GetProperty("HbrPotencyData", F)!.GetValue(power)!, System.Globalization.CultureInfo.InvariantCulture);
    }
    static Creature XExtraEnemy()
    {
        Creature enemy = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!;
        Set(enemy, "_currentHp", 100000000); Set(enemy, "_maxHp", 100000000); Set(Combat, "_enemies", Combat.Enemies.Append(enemy).ToList()); return enemy;
    }
    static async Task XActualCannon(CardModel source)
    {
        Creature cannon = (Creature)H("Actor", Blank<HotaruContestCannon>(), null, Player)!;
        Set(Combat, "_allies", new List<Creature> { Hero, cannon });
        var millsage = (Millsage)ModelDb.Relic<Millsage>().ToMutable(); Set(millsage, "_owner", Player); Set(Player, "_relics", Player.Relics.Append(millsage).ToList());
        await (Task<bool>)Call("HotaruCannonStatsHelper", "Damage", cannon, Enemy, .01m, new ThrowingPlayerChoiceContext(), source, null, 1)!;
    }
    static bool XAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    { __result = XAttackBody(__instance); return false; }
    static async Task<AttackCommand> XAttackBody(AttackCommand command)
    {
        var card = (CardModel)command.ModelSource!;
        await Hook.BeforeAttack(Combat, command);
        var targets = ((IReadOnlyList<Creature>)Native(command, "GetPossibleTargets")!).ToArray();
        decimal raw = (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(command)!;
        int hits = (int)Hook.ModifyAttackHitCount(Combat, command, (int)typeof(AttackCommand).GetField("_hitCount", F)!.GetValue(command)!);
        Call("HighHitDamageVisualHelper", "SetResolvedHitCount", command, (decimal)hits);
        XCardAttacks.Add(new(card, raw, hits, targets, Hero.HasPower<MindsEyePower>(), XPotency<HbrAttackUpPower>(),
            XPotency<HbrCriticalRateUpPower>(), XPotency<HbrCriticalDamageUpPower>(), (long)Call("IreneExposeCombatHelper", "GetDispelledKindCount", card.Owner)!));
        if (XCardThrow) throw new InvalidOperationException("controlled attack failure");
        for (int i = 0; i < hits; i++)
        {
            var results = (await Command(command.Attacker, raw, ValueProp.Move, card, targets)).ToArray();
            command.AddResultsInternal(results);
            foreach (DamageResult result in results) await NewGiven(card, result);
        }
        await Hook.AfterAttack(Combat, new ThrowingPlayerChoiceContext(), command); return command;
    }
    static bool XDamageLeaf(object[] __args, ref Task<IEnumerable<DamageResult>> __result)
    { __result = XDamageBody(__args); return false; }
    static async Task<IEnumerable<DamageResult>> XDamageBody(object[] args)
    {
        var targets = (IEnumerable<Creature>)Invoke(Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility", true)!.GetMethod("ReadTargets", F)!, null, new object?[] { args })!;
        Creature? dealer = args[4] as Creature; CardModel? card = args[5] as CardModel; ValueProp props = (ValueProp)args[3]; decimal raw = (decimal)args[2];
        var result = new List<DamageResult>(); int depth = XCardDepth++;
        try
        {
            foreach (Creature target in targets)
            {
                string? helper = card is ReadySteadyDieHbrgoSaki ? "CarolHeroCombatHelper" : card is ThereIsOnlyOneTruthHbrgoSaki ? "IreneExposeCombatHelper" : null;
                if (XCardNested && depth == 0 && helper != null) { XCardNested = false; await Command(dealer, .01m, ValueProp.Move, card, new[] { target }); }
                decimal amp = helper == null ? 0 : (decimal)Call(helper, "GetDamageAmplificationPercent", card, dealer, target, props)!;
                decimal resource = helper == null ? 1 : (decimal)Call(helper, card is ReadySteadyDieHbrgoSaki ? "GetHpDamageMultiplier" : "GetDpDamageMultiplier", card, target, dealer, props)!;
                decimal destruction = helper == null ? 1 : (decimal)Call(helper, "GetDestructionGainMultiplier", card, target, dealer, props)!;
                bool forced = card is ThereIsOnlyOneTruthHbrgoSaki && (bool)Call("IreneExposeCombatHelper", "ForcesCritical", target, dealer, card, props)!;
                decimal final = Actual(target, dealer, raw, props, card);
                await (Task)NativeHookBeforeReceive(target, dealer, final, props)!;
                decimal blocked = (target.PetOwner?.Creature ?? target).DamageBlockInternal(final, props);
                DamageResult hit = target.LoseHpInternal(Math.Max(0m, final - blocked), props);
                Set(hit, "<BlockedDamage>k__BackingField", (int)blocked); Call("ItsMygoIncomingDamageCapture", "AppendResults", result, new[] { hit });
                if (helper != null) { XCardCaptured ??= ExecutionContext.Capture(); XCardHits.Add(new(card!, target, raw, final, amp, resource, destruction, forced, depth)); }
                if (XDrainDp && depth == 0) { XDrainDp = false; Set(Player.GetRelic<DeflectorProtection>()!, "_dp", 0); }
            }
            return result;
        }
        finally { XCardDepth--; }
    }
    static void XRegister(Type type)
    { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
    static bool XSkipPile(ref bool skipVisuals) { skipVisuals = true; return true; }
    static void XAfterRemoved(PowerModel __instance, Creature oldOwner, ref Task __result)
    { if (XRecreateArtifact && __instance is ArtifactPower) { XRecreateArtifact = false; __result = XRecreateAfter(__result, oldOwner); } }
    static async Task XRecreateAfter(Task original, Creature target)
    { await original; await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), target, 2m, target, null); }
    static void XSaved(ThirtyOneBGardenCard card, string number)
    {
        GSaveType(card.GetType()); var options = new JsonSerializerOptions { IncludeFields = true };
        string json = JsonSerializer.Serialize(SavedProperties.From(card), options); var copy = (ThirtyOneBGardenCard)card.MutableClone();
        JsonSerializer.Deserialize<SavedProperties>(json, options)!.Fill(copy);
        Check(copy.UpgradeEffectOrder.SequenceEqual(card.UpgradeEffectOrder), number + " real SavedProperties JSON Fill preserves derived upgrade ledger");
        Check(!ReferenceEquals(copy.UpgradeEffectOrder, card.UpgradeEffectOrder), number + " cloned physical upgrade arrays do not alias");
    }
    static void XRun(string path, string number, Action cases)
    {
        bool ok = false; string? error = null; ulong? oldLocal = LocalContext.NetId;
        try
        {
            Install(); Localization(); LocalContext.NetId = 1UL; var h = new Harmony(ProductionId + ".cards" + number);
            foreach (Type type in FMembers.Concat(new[] { typeof(ReadySteadyDieHbrgoSaki), typeof(ThereIsOnlyOneTruthHbrgoSaki), typeof(XPlayObserver89), typeof(XDispelBuffA), typeof(XDispelBuffB), typeof(XDispelBuffC),
                typeof(ThirtyOneFCollection), typeof(ThirtyOneXCollection), typeof(HbrAttackUpPower), typeof(HbrCriticalRateUpPower), typeof(HbrCriticalDamageUpPower), typeof(MindsEyePower), typeof(HbrComboLargePower),
                typeof(HbrEnergyDebtPower), typeof(HighDimensionalShellPower), typeof(PhantomHighDimensionalShellPower), typeof(PhantomPower), typeof(MomentOfReign), typeof(HotaruContestCannon), typeof(Millsage), typeof(ArtifactPower), typeof(WeakPower), typeof(VulnerablePower), typeof(StrengthPower), typeof(MinionPower), typeof(Expose), typeof(Malaise), typeof(Overclock), typeof(BreakPower) })) XRegister(type);
            foreach (string name in new[] { "ExtraServingCanPlayPatch", "ExtraServingSpendResourcesPatch", "ExtraServingSpendEnergyPatch", "HbrEffectApplicationPatch", "HbrEffectInstanceStackingPatch", "HbrComboVirtualHitPatch",
                "HighHitDamageAttackScopePatch", "HighHitDamageCommandScopePatch", "HotaruCannonDamageContextPatch", "CreatureDamageBlockInternalPatch", "PhantomBeforeDamageReceivedPatch", "PhantomDamageTargetPatch",
                "ModeRestrictedCardPlayPatch", "CardPlayContextPatch", "MoreThanOneTruthPlayScopePatch", "IreneNativeExposeDispelPatch" })
                Check(h.CreateClassProcessor(Mod.GetTypes().Single(t => t.Name == name)).Patch().Count > 0, number + " installs production " + name);
            void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
            foreach (Type type in new[] { typeof(RunState), typeof(CombatState) }) h.Patch(type.GetMethod("IterateHookListeners", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FListeners), F)!) { priority = Priority.First + 200 });
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive)); Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip)); Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCreatureIntent", F)!, nameof(VisualTask));
            foreach (var method in typeof(Cmd).GetMethods(F).Where(m => (m.Name is "Wait" or "CustomScaledWait") && m.ReturnType == typeof(Task))) Patch(method, nameof(VisualTask));
            foreach (var method in typeof(SfxCmd).GetMethods(F).Where(m => m.Name == "Play")) Patch(method, nameof(Skip)); foreach (var method in typeof(ThinkCmd).GetMethods(F).Where(m => m.Name == "Play")) Patch(method, nameof(Skip));
            Patch(typeof(Godot.Time).GetMethod("GetTicksMsec", F)!, nameof(CrackClock)); Patch(typeof(CardPileCmd).GetMethod("AddDuringManualCardPlay", F)!, nameof(ChaseManualPile));
            foreach (var method in typeof(CardPileCmd).GetMethods(F).Where(m => m.Name == "Add" && m.GetParameters()[0].ParameterType == typeof(CardModel))) Patch(method, nameof(XSkipPile));
            Patch(typeof(AttackCommand).GetMethod("Execute", F)!, nameof(XAttackLeaf)); h.Unpatch(Canonical, typeof(EffectChecks).GetMethod(nameof(Terminal), F)!); Patch(Canonical, nameof(XDamageLeaf));
            h.Patch(typeof(PowerModel).GetMethod("AfterRemoved", F)!, postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(XAfterRemoved), F)!));
            try { cases(); ok = true; } finally { h.UnpatchAll(h.Id); }
        }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        finally { LocalContext.NetId = oldLocal; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), sourceNativeSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"), profile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual card bodies and native CanPlay/SpendResources/OnPlayWrapper/AutoPlay, PowerCmd applications/removals, hook dispatch, shared damage and critical arithmetic. Native AttackCommand animation and canonical HP terminal controlled; no Godot rendering/live multiplayer." }, new JsonSerializerOptions { WriteIndented = true }));
        if (!ok) throw new Exception("Inspect " + path); Console.WriteLine("PASS " + Passed.Count + " card" + number + " checks");
    }
}
