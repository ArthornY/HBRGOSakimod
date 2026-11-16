using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static partial class EffectChecks
{
    static readonly List<object> NativeProfileRows = [];
    static object ProfileProperty(object profile, string name) => profile.GetType().GetProperty(name, F)!.GetValue(profile)!;
    static AbstractModel ProfileModel(Type type, bool applier)
    {
        var model = (AbstractModel)RuntimeHelpers.GetUninitializedObject(type);
        Set(model, "<IsMutable>k__BackingField", true); Set(model, "<Id>k__BackingField", ModelDb.GetId(type));
        if (model is PowerModel power) { Set(power, "_owner", Hero); power.Applier = applier ? Enemy : Hero; Set(power, "_amount", 3); }
        else Set(model, "_owner", Player);
        return model;
    }
    static AbstractModel ProfileMutableModel(Type type, bool applier)
    {
        if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        AbstractModel canonical = ModelDb.GetById<AbstractModel>(ModelDb.GetId(type));
        Set(canonical, "<Id>k__BackingField", ModelDb.GetId(type));
        AbstractModel model = canonical.MutableClone();
        if (model is PowerModel power) { Set(power, "_owner", Hero); power.Applier = applier ? Enemy : Hero; }
        else if (model is EventModel) Set(model, "<Owner>k__BackingField", Player);
        else Set(model, "_owner", Player);
        return model;
    }
    static object ProfileTargets(Type type, Creature target)
    {
        Type element = type.GetGenericArguments()[0]; Array result = Array.CreateInstance(element, 1);
        result.SetValue(element == typeof(Creature) ? target : Activator.CreateInstance(element, target), 0);
        return result;
    }
    static async Task ProfileWrapper(AbstractModel source, Creature target, decimal raw, ValueProp props, Creature? dealer)
    {
        Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrNativeEffectSkillDamagePatch", true)!;
        MethodInfo native = typeof(CreatureCmd).GetMethods(F).Where(method => method.Name == "Damage" &&
            method.GetParameters() is var parameters && parameters.Length >= 6 && parameters[1].ParameterType == typeof(Creature) &&
            parameters[2].ParameterType == typeof(decimal) && parameters[4].ParameterType == typeof(Creature))
            .OrderByDescending(method => method.GetParameters().Length).First();
        MethodInfo wrapper = (MethodInfo)Invoke(patch.GetMethod("CreateWrapper", F)!, null, native, typeof(AbstractModel))!;
        object?[] args = native.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(PlayerChoiceContext) ? (object)new ThrowingPlayerChoiceContext() :
            parameter.Position == 1 ? target :
            parameter.ParameterType == typeof(decimal) ? raw : parameter.ParameterType == typeof(ValueProp) ? props :
            parameter.ParameterType == typeof(Creature) ? dealer : parameter.HasDefaultValue ? parameter.DefaultValue : null).Append(source).ToArray();
        await (Task)Invoke(wrapper, null, args)!;
    }
    static async Task NativeDamageProfileCases()
    {
        var harmony = new Harmony(ProductionId + ".native-profiled-damage");
        var patched = new List<MethodBase>(); ExceptionDispatchInfo? failure = null;
        try
        {
            Type registry = Helper("HbrNativeDamageProfiles");
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => method.Name is "Wait" or "CustomScaledWait" && method.ReturnType == typeof(Task)))
            { harmony.Patch(wait, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask), F)!)); patched.Add(wait); }
            object[] profiles = ((IEnumerable)registry.GetProperty("All", F)!.GetValue(null)!).Cast<object>().ToArray();
            NativeProfileGrantingInventory(profiles);
            Check(profiles.Length >= 25, "native damage catalog contains the audited stable raw-skill/world sources");
            Type direct = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrNativeProfiledDamagePatch", true)!;
            MethodBase[] bodies = ((IEnumerable<MethodBase>)Invoke(direct.GetMethod("TargetMethods", F)!, null)!).ToArray();
            Check(bodies.Length >= 20 && bodies.Distinct().Count() == bodies.Length,
                "audited native profile patch resolves every decimal body once, including optional beta sources");
            MethodInfo transpiler = direct.GetMethod("Transpiler", F)!;
            foreach (MethodBase body in bodies)
            {
                CodeInstruction[] original = PatchProcessor.GetOriginalInstructions(body).ToArray();
                int commands = original.Count(instruction => instruction.operand is MethodInfo method && method.DeclaringType == typeof(CreatureCmd) &&
                    method.Name == "Damage" && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(decimal)));
                CodeInstruction[] result = ((IEnumerable<CodeInstruction>)Invoke(transpiler, null, original, body)!).ToArray();
                Check(commands > 0 && result.Count(instruction => instruction.operand is MethodInfo method && method.Name.StartsWith("HbrEffectDamage_")) == commands,
                    "all exact native decimal call sites are wrapped in " + body.DeclaringType?.FullName + "." + body.Name);
            }
            harmony.CreateClassProcessor(direct).Patch(); patched.AddRange(bodies);
            foreach (object profile in profiles)
            {
                Type type = (Type)ProfileProperty(profile, "ModelType");
                string basis = ProfileProperty(profile, "Basis").ToString()!;
                bool applier = ProfileProperty(profile, "StatSource").ToString() == "Applier";
                if (basis == "FixedCost")
                {
                    foreach (bool hbr in new[] { true, false })
                    {
                        Reset(hbr); AbstractModel cost = ProfileMutableModel(type, false);
                        var vars = cost is CardModel cardCost ? cardCost.DynamicVars : ((EventModel)cost).DynamicVars;
                        DynamicVar variable = vars[type.Name == "ThermaeNonupeipe" ? "DrinkHpLoss" : "HpLoss"];
                        Equal((decimal)Call("HbrNativeFixedResourceHelper", "ReadBase", variable)!, variable.BaseValue * (hbr ? 100m : 1m),
                            "optional beta HP-cost profile uses the fixed resource scale instead of skill conversion: " + type.Name + "/" + hbr);
                    }
                    continue;
                }
                if (basis is not ("SkillRaw" or "WorldAmount")) continue;
                foreach (bool hbr in new[] { true, false })
                {
                    Reset(hbr); AbstractModel model = ProfileModel(type, applier);
                    Creature target = applier ? Hero : Enemy;
                    decimal raw = basis == "WorldAmount" ? 500m : 3m;
                    await ProfileWrapper(model, target, raw, ValueProp.Unpowered, Hero);
                    Hit hit = Hits.Single();
                    Check(hit.Raw == raw && hit.Props == ValueProp.Unpowered && hit.Enabled == hbr,
                        "profile boundary preserves native raw amount/flags and only activates in HBR: " + type.Name + "/" + hbr);
                    if (!hbr) Equal(hit.Final, raw, "legacy saved profile remains literal for " + type.Name);
                    else
                    {
                        decimal actual = hit.Final; Hits.Clear();
                        Set(RunState, "<Rng>k__BackingField", new MegaCrit.Sts2.Core.Runs.RunRngSet(NonCriticalSeed));
                        IDisposable scope = basis == "WorldAmount"
                            ? (IDisposable)Call("HbrEffectSkillDamageContext", "BeginWorld", target, Hero, null, raw, ValueProp.Unpowered, applier ? Enemy : Hero)!
                            : Begin(target, Hero, null, raw, ValueProp.Unpowered, applier ? Enemy : Hero);
                        using (scope) await Command(Hero, raw, ValueProp.Unpowered, null, new[] { target });
                        Equal(actual, Hits.Single().Final, "profile selects exactly its declared owner/applier and raw/world formula for " + type.Name);
                        if (basis == "WorldAmount") Check(actual < raw * 10m,
                            "world-derived native " + type.Name + " never receives an accidental thousandfold skill-unit conversion");
                        NativeProfileRows.Add(new { model = type.FullName, basis, applier, raw, actual });
                    }
                }
            }
            Reset(); Type inferno = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.InfernoPower", true)!;
            await ProfileWrapper(ProfileModel(inferno, false), Hero, 100m, ValueProp.Unpowered, Hero);
            Check(!Hits.Single().Enabled && Hits.Single().Final == 100m, "Inferno's already-scaled owner self-cost is not classified as outgoing skill damage");
            await NativeProfileRealBodies();
            await NativeProfileTypedBodies();
            await NativeProfilePresentationCases(harmony, patched);
            Cases.Add(new { source = "native damage profile catalog", profileCount = profiles.Length, wrappedBodies = bodies.Length,
                profiles = profiles.Select(profile => new { model = ((Type)ProfileProperty(profile, "ModelType")).FullName,
                    basis = ProfileProperty(profile, "Basis").ToString(), statSource = ProfileProperty(profile, "StatSource").ToString(),
                    ownerSelfIsFixedCost = (bool)ProfileProperty(profile, "OwnerSelfIsFixedCost"),
                    usesAmountAsDamage = (bool)ProfileProperty(profile, "UsesAmountAsDamage") }), rows = NativeProfileRows });
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            foreach (MethodBase method in patched.Distinct())
                try { harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); }
                catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
            Reset();
        }
        failure?.Throw();
    }

    static void NativeProfileGrantingInventory(object[] profiles)
    {
        HashSet<Type> powers = profiles.Select(profile => (Type)ProfileProperty(profile, "ModelType"))
            .Where(type => typeof(PowerModel).IsAssignableFrom(type)).Append(typeof(MegaCrit.Sts2.Core.Models.Powers.ThornsPower)).ToHashSet();
        Type[] nativeTypes = typeof(CardModel).Assembly.GetTypes();
        var rows = new List<object>();
        foreach (Type cardType in nativeTypes.Where(type => !type.IsAbstract && typeof(CardModel).IsAssignableFrom(type)))
        {
            var applied = new HashSet<string>();
            foreach (Type bodyType in nativeTypes.Where(type => type == cardType || type.FullName!.StartsWith(cardType.FullName + "+", StringComparison.Ordinal)))
            foreach (MethodInfo body in bodyType.GetMethods(F | BindingFlags.DeclaredOnly).Where(method => !method.ContainsGenericParameters && method.GetMethodBody() != null))
            foreach (MethodInfo call in PatchProcessor.GetOriginalInstructions(body).Select(instruction => instruction.operand).OfType<MethodInfo>())
                if (call.IsGenericMethod && call.DeclaringType == typeof(PowerCmd))
                    foreach (Type power in call.GetGenericArguments().Where(powers.Contains)) applied.Add(power.FullName!);
            if (applied.Count == 0) continue;
            CardModel card = (CardModel)ProfileMutableModel(cardType, false);
            Check((bool)Call("HbrNativeDelayedDamageVariables", "Contains", card)! ||
                cardType.Name is "Abrasive" or "Reflect" or "NecroMastery",
                "every native card applying an audited damage power has an exact future-damage mapping or explicit stack/world-resource exemption: " + cardType.Name);
            rows.Add(new { card = cardType.FullName, appliedPowers = applied,
                variables = card.DynamicVars.Values.Select(value => new { value.Name, type = value.GetType().FullName, value.BaseValue }) });
        }
        File.WriteAllText(Path.Combine(ReportDirectory, "native-granting-cards.json"),
            System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    static async Task NativeProfileRealBodies()
    {
        Reset();
        Type flame = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.FlameBarrierPower", true)!;
        PowerModel power = (PowerModel)ProfileModel(flame, false);
        await (Task)Native(power, "AfterDamageReceived", new ThrowingPlayerChoiceContext(), Hero, Blank<DamageResult>(), ValueProp.Move, Enemy, null)!;
        Check(Hits.Count == 1 && Hits[0].Enabled && Hits[0].Raw == 3m && Hits[0].Props == ValueProp.Unpowered && Hits[0].Final > 3m,
            "real native Flame Barrier retaliation body reaches the profiled skill wrapper without changing retaliation flags");
        Reset();
        Type rolling = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.RollingBoulderPower", true)!;
        power = (PowerModel)ProfileModel(rolling, false);
        await (Task)Invoke(rolling.GetMethod("DoDamage", F)!, power, new ThrowingPlayerChoiceContext(), new[] { Enemy })!;
        Check(Hits.Count == 1 && Hits[0].Enabled && Hits[0].Raw == 3m && Hits[0].Final > 3m,
            "real synchronous Rolling Boulder DoDamage body is covered rather than only scanning async state machines");
        Reset();
        Type magic = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.MagicBombPower", true)!;
        power = (PowerModel)ProfileModel(magic, true);
        Powers(Hero).Add(power);
        var result = (Task)Native(power, "AfterSideTurnEnd", new ThrowingPlayerChoiceContext(), Hero.Side, new[] { Hero })!;
        // Its native Wait/Flash are managed visual leaves installed below by
        // this fixture's common host; real damage and removal remain native.
        await result;
        Check(Hits.Count == 1 && Hits[0].Enabled && Hits[0].Target == Hero && Hits[0].Dealer == Hero,
            "real native Magic Bomb keeps target-as-dealer attribution while its skill source is the opposing applier");
    }

    static async Task NativeProfileTypedBodies()
    {
        foreach (string name in new[] { "Cards.CaptureSpirit", "Relics.Tingsha", "Relics.Metronome", "Powers.TheBombPower", "Powers.InsatiableDominionPower" })
        {
            Type? type = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models." + name, false);
            if (type == null) continue;
            Reset(); bool applier = type.Name == "InsatiableDominionPower";
            AbstractModel model = ProfileMutableModel(type, applier);
            var vars = model switch { CardModel card => card.DynamicVars, RelicModel relic => relic.DynamicVars, PowerModel power => power.DynamicVars, _ => throw new Exception(name) };
            DamageVar variable = vars.Damage;
            MethodInfo adapter = type.GetMethods(F | BindingFlags.DeclaredOnly)
                .Select(method => method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType)
                .Where(state => state != null).Select(state => AccessTools.Method(state, "MoveNext"))
                .SelectMany(body => PatchProcessor.GetOriginalInstructions(body)).Select(instruction => instruction.operand).OfType<MethodInfo>()
                .First(method => method.DeclaringType == typeof(CreatureCmd) && method.Name == "Damage" &&
                    method.GetParameters().Any(parameter => parameter.ParameterType == typeof(DamageVar)));
            Creature target = applier ? Hero : Enemy;
            object?[] args = adapter.GetParameters().Select(parameter =>
                parameter.ParameterType == typeof(PlayerChoiceContext) ? (object)new ThrowingPlayerChoiceContext() :
                parameter.ParameterType == typeof(DamageVar) ? variable :
                typeof(IEnumerable<Creature>).IsAssignableFrom(parameter.ParameterType) ? new[] { target } :
                parameter.ParameterType == typeof(Creature) ? parameter.Name == "target" ? target : model is PowerModel && applier ? null : Hero :
                parameter.ParameterType == typeof(CardModel) ? model as CardModel : parameter.HasDefaultValue ? parameter.DefaultValue : null).ToArray();
            await (Task)Invoke(adapter, null, args)!;
            Check(Hits.Count == 1 && Hits[0].Enabled && Hits[0].Raw == variable.BaseValue && Hits[0].Final > variable.BaseValue,
                "real typed DamageVar adapter declares the new native profile " + name + " without rewriting storage");
        }
    }

    static async Task NativeProfilePresentationCases(Harmony harmony, List<MethodBase> patched)
    {
        Reset(); Localization();
        Type presentation = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrNativeProfiledEffectDescriptionPatch", true)!;
        MethodBase[] methods = ((IEnumerable<MethodBase>)Invoke(presentation.GetMethod("TargetMethods", F)!, null)!).ToArray();
        harmony.CreateClassProcessor(presentation).Patch(); patched.AddRange(methods);
        var tables = (Dictionary<string, LocTable>)typeof(LocManager).GetField("_tables", F)!.GetValue(LocManager.Instance)!;
        tables["orbs"] = new LocTable("orbs", System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText("D:/stsmod/sts/localization/zhs/orbs.json"))!, null!);
        string PowerText(PowerModel power)
        {
            LocString text = power.SmartDescription; text.Add("OwnerName", "测试角色");
            power.DynamicVars.AddTo(text);
            Invoke(AccessTools.Method(typeof(PowerModel), "AddDumbVariablesToDescription"), power, text, null);
            return text.GetFormattedText();
        }
        Type native = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Orbs.LightningOrb", true)!;
        OrbModel orb = (OrbModel)ProfileMutableModel(native, false);
        decimal passive = orb.PassiveVal, evoke = orb.EvokeVal;
        string text = orb.SmartDescription.GetFormattedText();
        Check(text.Contains((passive * 1000m).ToString("0")) && text.Contains((evoke * 1000m).ToString("0")) &&
            !text.Contains('%') && !text.Contains("倍率") && !text.Contains('{'),
            "actual native Lightning Orb smart tooltip shows numeric passive/evoke damage bases without coefficient language");
        Check(orb.PassiveVal == passive && orb.EvokeVal == evoke,
            "formatting a native orb never rewrites its native passive or evoke storage");
        native = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.FlameBarrierPower", true)!;
        PowerModel flame = (PowerModel)ProfileMutableModel(native, false); Set(flame, "_amount", 3);
        text = PowerText(flame);
        Check(text.Contains("3000") && !text.Contains('%') && !text.Contains("倍率") && flame.Amount == 3,
            "actual native Flame Barrier smart tooltip converts its damage-valued Amount to numeric damage while retaining its three-stack storage");
        var enemyThorns = Power<MegaCrit.Sts2.Core.Models.Powers.ThornsPower>(Enemy, 2);
        text = PowerText(enemyThorns);
        Check(text.Contains("200") && !text.Contains("2000") && !text.Contains('%') && enemyThorns.Amount == 2,
            "untargeted enemy Thorns tooltip uses the incoming hundred-point skill basis rather than the player's thousand-point basis");
        native = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.PanachePower", true)!;
        PowerModel panache = (PowerModel)ProfileMutableModel(native, false); Set(panache, "_amount", 9);
        decimal cardsLeft = panache.DynamicVars["CardsLeft"].BaseValue;
        text = PowerText(panache);
        Check(text.Contains("9000") && text.Contains("[blue]" + cardsLeft.ToString("0") + "[/blue]张") &&
            panache.Amount == 9 && panache.DisplayAmount == cardsLeft,
            "Panache numeric damage reads Amount rather than its separate icon progress counter and preserves cards-left text");
        native = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.TheBombPower", true)!;
        PowerModel bomb = (PowerModel)ProfileMutableModel(native, false); Set(bomb, "_amount", 3);
        decimal damage = bomb.DynamicVars.Damage.BaseValue;
        text = PowerText(bomb);
        Check(text.Contains((damage * 1000m).ToString("0")) && text.Contains("[blue]3[/blue]") &&
            !text.Contains("[blue]3000[/blue]回合") && !text.Contains('%') && bomb.Amount == 3 && bomb.DynamicVars.Damage.BaseValue == damage,
            "The Bomb converts its separate Damage variable while its Amount remains a three-turn countdown");
        native = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards.CaptureSpirit", true)!;
        CardModel card = (CardModel)ProfileMutableModel(native, false);
        Player.PlayerCombatState!.PlayPile.AddInternal(card, -1, true);
        decimal raw = card.DynamicVars.Damage.BaseValue;
        string CardText(bool detail, Creature? target)
        {
            using var scope = (IDisposable)Invoke(Helper("HbrCardDescriptionContext").GetMethods(F)
                .Single(method => method.Name == "Begin" && method.GetParameters().Length == 4), null, card, detail, false, target)!;
            LocString description = card.Description; card.DynamicVars.AddTo(description);
            return description.GetFormattedText();
        }
        string basisText = CardText(false, null);
        Check(basisText.Contains((raw * 1000m).ToString("0")) && !basisText.Contains('%') && !basisText.Contains("倍率"),
            "untargeted native Capture Spirit face honestly shows numeric skill base rather than an invented target calculation");
        Hits.Clear(); using (Begin(Enemy, Hero, card, raw, card.DynamicVars.Damage.Props, Hero))
            await Command(Hero, raw, card.DynamicVars.Damage.Props, card);
        decimal actual = Hits.Single().Final;
        string face = CardText(false, Enemy), detail = CardText(true, Enemy);
        string coefficient = (raw / 0.06m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        Check(face.Contains(actual.ToString("0")) && !face.Contains('%') && !face.Contains("倍率") &&
            detail.Contains(actual.ToString("0")) && detail.Contains("（" + coefficient + "%）") && !detail.Contains("倍率"),
            "native Capture Spirit selected-target face matches the actual attribute pipeline and inspect adds only a parenthesized coefficient");
        Check(card.DynamicVars.Damage.BaseValue == raw && RngCount(RunState.Rng.Niche) == 1,
            "native card numeric preview leaves raw DamageVar intact and does not consume additional critical RNG");
        H("Mode", false, 1);
        LocString legacyOrb = orb.SmartDescription; legacyOrb.Add("Passive", orb.PassiveVal); legacyOrb.Add("Evoke", orb.EvokeVal);
        Check(!PowerText(flame).Contains("3000") && !legacyOrb.GetFormattedText().Contains((evoke * 1000m).ToString("0")),
            "legacy saved mode retains native power and orb tooltip numbers");
        NativeDelayedGrantingCardCases();
    }

    static void NativeDelayedGrantingCardCases()
    {
        Reset();
        Type registry = Helper("HbrNativeDelayedDamageVariables");
        object[] entries = ((IEnumerable)registry.GetProperty("All", F)!.GetValue(null)!).Cast<object>().ToArray();
        var rows = new List<object>();
        Check(entries.Length >= 15, "native delayed damage cache includes every audited stable future-damage card without classifying all PowerVars");
        foreach (object entry in entries)
        {
            Type type = (Type)entry.GetType().GetProperty("Key")!.GetValue(entry)!;
            object[] fields = ((IEnumerable)entry.GetType().GetProperty("Value")!.GetValue(entry)!).Cast<object>().ToArray();
            CardModel card = (CardModel)ProfileMutableModel(type, false);
            var before = card.DynamicVars.Values.ToDictionary(variable => variable.Name, variable => variable.BaseValue);
            foreach (object field in fields)
            {
                string name = (string)ProfileProperty(field, "Name");
                Type power = (Type)ProfileProperty(field, "AppliedPowerType");
                Check(before.ContainsKey(name) && typeof(PowerModel).IsAssignableFrom(power) &&
                    ProfileProperty(field, "Target").ToString() == "FutureEnemy",
                    "exact delayed card field is bound to its native power and future-enemy role: " + type.Name + "." + name);
                Equal((decimal)Call("HbrNativeFixedResourceHelper", "ReadBase", card.DynamicVars[name])!, before[name],
                    "granting a future skill never scales the transferred native amount as an HP resource: " + type.Name + "." + name);
            }
            bool nativeTextAvailable = LocString.Exists("cards", card.Id.Entry + ".description");
            if (nativeTextAvailable)
            {
                string Text(bool detail)
                {
                    using var scope = (IDisposable)Invoke(Helper("HbrCardDescriptionContext").GetMethods(F)
                        .Single(method => method.Name == "Begin" && method.GetParameters().Length == 4), null, card, detail, false, Hero)!;
                    LocString text = card.Description; card.DynamicVars.AddTo(text);
                    text.Add("singleStarIcon", "★"); text.Add("energyPrefix", "energy");
                    return text.GetFormattedText();
                }
                string face = Text(false), detail = Text(true);
                Check(!face.Contains("倍率") && !face.Contains('%') && !face.Contains('{'),
                    "actual native granting card face contains numeric future damage without coefficient wording: " + type.Name);
                foreach (object field in fields)
                {
                    decimal raw = before[(string)ProfileProperty(field, "Name")];
                    string coefficient = (raw / 0.06m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    Check(face.Contains((raw * 1000m).ToString("0")) && detail.Contains("（" + coefficient + "%）"),
                        "native granting card ignores its self target for an unknown future enemy and exposes coefficient only in inspect: " + type.Name);
                }
                if (type.Name == "TheBomb") Check(face.Contains("3") && before["Turns"] == 3m,
                    "native The Bomb card retains its three-turn count while converting only BombDamage");
            }
            Check(card.DynamicVars.Values.All(variable => before[variable.Name] == variable.BaseValue),
                "native granting card formatting leaves all raw damage and progress variables unchanged: " + type.Name);
            rows.Add(new { card = type.FullName, nativeTextAvailable,
                fields = fields.Select(field => new { name = ProfileProperty(field, "Name"), power = ((Type)ProfileProperty(field, "AppliedPowerType")).FullName,
                    target = ProfileProperty(field, "Target").ToString(), props = ProfileProperty(field, "Props").ToString() }) });
        }
        foreach ((string name, string variable) in new[] { ("Abrasive", "ThornsPower"), ("Outbreak", "PoisonPower"),
            ("Reflect", "Block"), ("NecroMastery", "Summon") })
        {
            Type type = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards." + name, true)!;
            CardModel card = (CardModel)ProfileMutableModel(type, false);
            if (name == "Outbreak" && card.DynamicVars.Values.Any(value => value.Name == "OutbreakPower")) continue;
            Check(!(bool)Call("HbrNativeDelayedDamageVariables", "Contains", card)!,
                "native stack/summon/block-only granting card is outside fixed future-damage mapping: " + name + "." + variable);
        }
        Cases.Add(new { source = "native future-damage card field cache", rows });
    }
}
