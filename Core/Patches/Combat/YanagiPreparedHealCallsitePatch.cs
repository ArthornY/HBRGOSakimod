using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Bind the actual card at its own healing call site. A potion, relic, power,
// stage reset or max-HP structural command cannot borrow an ambient card play.
[HarmonyPatch]
internal static class YanagiPreparedHealCallsitePatch
{
    internal sealed record Site(MethodInfo Caller, Type CardType, FieldInfo Source, FieldInfo Play, int Count);
    internal sealed record Catalog(Site[] Sites, string[] Reviews, int Scanned, double Milliseconds);
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private static readonly MethodInfo NativeHeal = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Heal),
        new[] { typeof(Creature), typeof(decimal), typeof(bool) });
    private static readonly MethodInfo Adapter = AccessTools.Method(typeof(YanagiPreparedHealCallsitePatch), nameof(Heal));
    private static readonly Lazy<Catalog> Cached = new(Build);
    internal static Catalog Coverage => Cached.Value;
    private static IEnumerable<MethodBase> TargetMethods()
    {
        if (Coverage.Reviews.Length != 0) throw new InvalidOperationException("Unreviewed prepared card HP healing: " + string.Join("; ", Coverage.Reviews));
        return Coverage.Sites.Select(site => site.Caller);
    }
    private static Catalog Build()
    {
        Stopwatch watch = Stopwatch.StartNew();
        List<Site> sites = new(); List<string> reviews = new(); HashSet<MethodInfo> seen = new(); int scanned = 0;
        foreach (Assembly assembly in new[] { typeof(CardModel).Assembly, typeof(YanagiPreparedHealCallsitePatch).Assembly }.Distinct())
        foreach (Type card in assembly.GetTypes().Where(type => typeof(CardModel).IsAssignableFrom(type)))
        {
            MethodInfo onPlay = AccessTools.Method(card, "OnPlay");
            if (onPlay == null || onPlay.IsAbstract || !seen.Add(onPlay)) continue;
            Type state = onPlay.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
            MethodInfo body = state == null ? onPlay : AccessTools.DeclaredMethod(state, "MoveNext");
            if (body?.GetMethodBody() == null) continue;
            scanned++;
            var instructions = PatchProcessor.GetOriginalInstructions(body);
            int count = instructions.Count(instruction => instruction.operand is MethodInfo called && called == NativeHeal);
            if (count == 0) continue;
            string name = body.DeclaringType.FullName + "." + body.Name;
            if (body.ContainsGenericParameters) { reviews.Add(name + " is generic"); continue; }
            if (instructions.Any(instruction => instruction.operand is MethodInfo called && called.DeclaringType == typeof(CreatureCmd) && called.Name == nameof(CreatureCmd.GainMaxHp)))
            { reviews.Add(name + " contains both max-HP structural change and direct healing"); continue; }
            FieldInfo source = state?.GetFields(Declared).FirstOrDefault(field => field.Name == "<>4__this" && typeof(CardModel).IsAssignableFrom(field.FieldType));
            FieldInfo play = source == null ? state?.GetFields(Declared).FirstOrDefault(field => field.FieldType == typeof(CardPlay)) : null;
            if (state != null && source == null && play == null) { reviews.Add(name + " has no card or CardPlay capture"); continue; }
            sites.Add(new(body, onPlay.DeclaringType, source, play, count));
        }
        watch.Stop(); return new(sites.ToArray(), reviews.ToArray(), scanned, watch.Elapsed.TotalMilliseconds);
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        Site site = Coverage.Sites.Single(item => item.Caller == __originalMethod);
        int changed = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is not MethodInfo called || called != NativeHeal) { yield return instruction; continue; }
            CodeInstruction owner = new(OpCodes.Ldarg_0);
            owner.labels.AddRange(instruction.labels); instruction.labels.Clear();
            owner.blocks.AddRange(instruction.blocks); instruction.blocks.Clear();
            yield return owner;
            if (site.Source != null) yield return new(OpCodes.Ldfld, site.Source);
            else if (site.Play != null)
            {
                yield return new(OpCodes.Ldfld, site.Play);
                yield return new(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(CardPlay), nameof(CardPlay.Card)));
            }
            yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = Adapter };
            changed++;
        }
        if (changed != site.Count) throw new InvalidOperationException($"Prepared heal call-site count changed in {__originalMethod}: {changed}/{site.Count}");
    }
    private static Task Heal(Creature creature, decimal amount, bool playAnim, CardModel source)
    {
        return CreatureCmd.Heal(creature, YanagiButlerCombatHelper.ScaleRecovery(source, amount), playAnim);
    }
}
