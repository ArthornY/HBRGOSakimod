using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Native fixed effects which use literal values or power stacks instead of
// resource DynamicVars. Each row is an audited semantic boundary, not a global
// command prefix: percent heals, life steal, phase resets and damage stay out.
[HarmonyPatch]
internal static class HbrNativeFixedResourceCommandPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return RegenBody();
        yield return Body(typeof(PaperCutsPower), nameof(PaperCutsPower.AfterDamageGiven));
        yield return Body(typeof(Terminal), nameof(Terminal.AfterRoomEntered));
        yield return Body(typeof(NightTerrors), nameof(NightTerrors.AfterRestSiteHeal));
        yield return Body(typeof(CookRestSiteOption), nameof(CookRestSiteOption.OnSelect));
        yield return Body(typeof(KnowledgeDemon), "PonderMove");
        yield return Body(typeof(WaterfallGiant), "SiphonMove");
    }

    private static MethodInfo RegenBody()
    {
        // Beta moved regeneration to an earlier hook. Resolving an inherited
        // empty AfterSideTurnEnd would silently miss its actual healing body.
        MethodInfo body = null;
        foreach (string name in new[] { "AfterSideTurnEnd", "BeforeSideTurnEndEarly" })
        {
            MethodInfo method = typeof(RegenPower).GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Type state = method?.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
            if (state == null) continue;
            if (body != null) throw new InvalidOperationException("Expected one declared native Regen healing hook.");
            body = AccessTools.Method(state, "MoveNext");
        }
        return body ?? throw new MissingMethodException("Missing declared native Regen healing hook.");
    }

    private static MethodInfo Body(Type type, string name)
    {
        MethodInfo method = AccessTools.Method(type, name);
        Type state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        return state == null ? method : AccessTools.Method(state, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo call && call.DeclaringType == typeof(CreatureCmd))
            {
                string name = call.Name switch
                {
                    nameof(CreatureCmd.Heal) => nameof(Heal),
                    nameof(CreatureCmd.GainMaxHp) => nameof(GainMaxHp),
                    nameof(CreatureCmd.LoseMaxHp) => nameof(LoseMaxHp),
                    _ => null
                };
                if (name != null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(HbrNativeFixedResourceCommandPatch), name);
                }
            }
            yield return instruction;
        }
    }

    private static Task Heal(Creature creature, decimal amount, bool playAnim) =>
        CreatureCmd.Heal(creature, HbrResourceValueHelper.Fixed(creature, amount), playAnim);
    private static Task GainMaxHp(Creature creature, decimal amount) =>
        CreatureCmd.GainMaxHp(creature, HbrResourceValueHelper.Fixed(creature, amount));
    private static Task LoseMaxHp(PlayerChoiceContext choiceContext, Creature creature, decimal amount, bool showVfx) =>
        CreatureCmd.LoseMaxHp(choiceContext, creature, HbrResourceValueHelper.Fixed(creature, amount), showVfx);
}
