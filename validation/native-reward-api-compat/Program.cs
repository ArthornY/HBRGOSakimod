using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

static class Checks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static;
    static readonly List<string> Passed = [];
    static Type Factory = null!;

    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Passed.Add(name);
    }

    static object? Call(string name, params object?[] arguments)
    {
        try { return Factory.GetMethod(name, F)!.Invoke(null, arguments); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    static object RunDefault(RunManager manager, string name)
    {
        PropertyInfo property = typeof(RunManager).GetProperty(name, F)!;
        object? value = property.GetValue(manager);
        if (value is not null) return value;
        value = RuntimeHelpers.GetUninitializedObject(property.PropertyType);
        if (property.SetMethod is not null) property.SetValue(manager, value);
        else AccessTools.Field(typeof(RunManager), "<" + name + ">k__BackingField")!.SetValue(manager, value);
        return value;
    }

    static void RunCases()
    {
        Factory = Assembly.LoadFrom(Bootstrap.ModPath)
            .GetType("HBRGOSakiMod.Core.Compatibility.NativeRewardApiCompatibility", true)!;
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        Type relicType = typeof(AbstractModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Relics.BurningBlood", true)!;
        var relic = (RelicModel)RuntimeHelpers.GetUninitializedObject(relicType);
        AccessTools.Field(typeof(AbstractModel), "<IsMutable>k__BackingField")!.SetValue(relic, true);
        Check(player.PlayerRng is null, "constructor receives a player without an initialized RNG set");

        var reward = (RelicReward)Call("Relic", relic, player)!;
        Check(reward.GetType() == typeof(RelicReward), "factory creates the actual native RelicReward");
        Check(ReferenceEquals(reward.Player, player), "relic reward retains player identity");
        Check(ReferenceEquals(reward.Relic, relic), "predetermined relic identity retained");
        Check(ReferenceEquals(AccessTools.Field(typeof(RelicReward), "_predeterminedRelic")!.GetValue(reward), relic),
            "native predetermined relic field is retained");
        Check(AccessTools.Field(typeof(Reward), "_rngOverride")!.GetValue(reward) is null,
            "native optional RNG override remains null instead of forcing the player rewards stream");
        Check(reward.IsPopulated, "predetermined native reward is already populated");
        reward.Populate();
        Check(ReferenceEquals(reward.Relic, relic), "Populate does not reroll a predetermined relic");
        Check(player.PlayerRng is null, "construction and Populate do not create or consume a player RNG set");

        var immutable = (RelicModel)RuntimeHelpers.GetUninitializedObject(relicType);
        try { Call("Relic", immutable, player); throw new Exception("Canonical relic unexpectedly accepted"); }
        catch (Exception exception) when (exception.GetType().Name == "CanonicalModelException")
        {
            Check(exception is not TargetInvocationException, "native constructor error is propagated without reflection wrapping");
        }

        RunManager manager = RunManager.Instance;
        object synchronizer = RunDefault(manager, "PlayerChoiceSynchronizer");
        PropertyInfo? stackProperty = typeof(RunManager).GetProperty("CardSelectorStack", F);
        object? selectorStack = stackProperty is null ? null : RunDefault(manager, "CardSelectorStack");
        CardCreationOptions options = new(Array.Empty<CardPoolModel>(),
            CardCreationSource.Other, CardRarityOddsType.Uniform, null);
        var cardReward = (CardReward)Call("Card", options, 3, player)!;
        Check(cardReward.GetType() == typeof(CardReward), "factory creates the actual native CardReward");
        Check(ReferenceEquals(cardReward.Player, player), "card reward retains player identity");
        Check((int)typeof(CardReward).GetProperty("OptionCount", F)!.GetValue(cardReward)! == 3,
            "native card reward retains number of choices");
        Check(ReferenceEquals(AccessTools.Field(typeof(CardReward), "_synchronizer")!.GetValue(cardReward), synchronizer),
            "null optional synchronizer falls back to actual RunManager synchronizer");
        if (stackProperty is not null)
        {
            Check(selectorStack is not null, "beta RunManager has an actual selector stack default");
            Check(ReferenceEquals(AccessTools.Field(typeof(CardReward), "_selectorStack")!.GetValue(cardReward), selectorStack),
                "null optional selectorStack falls back to actual RunManager stack");
        }
        else
        {
            Check(AccessTools.Field(typeof(CardReward), "_selectorStack") is null,
                "stable constructor has no invented selector-stack field");
        }
    }

    public static int Run()
    {
        Exception? error = null;
        try { RunCases(); }
        catch (Exception exception) { error = exception; }
        var report = new
        {
            ok = error is null, checks = Passed.Count, passed = Passed,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Bootstrap.ModPath))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Reward).Assembly.Location))),
            error = error?.ToString(),
            boundary = "Calls production reward factory and actual native CardReward/RelicReward constructors and predetermined Populate. No constructor spies or fake native types; uninitialized player/relic/synchronizer sentinels avoid running a game. No card choice UI, random reward rolling, save/load or Godot rendering."
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Bootstrap.ReportPath)!);
        File.WriteAllText(Bootstrap.ReportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(error is null ? "PASS" : "FAIL")} {Passed.Count} native reward API checks");
        if (error is not null) Console.Error.WriteLine(error);
        return error is null ? 0 : 1;
    }
}
