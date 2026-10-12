using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(SkittishPower), nameof(SkittishPower.AfterAttack))]
internal static class PhantomSkittishPowerPatch
{
    private const string RetractSfx =
        "event:/sfx/enemy/enemy_attacks/phantasmal_gardeners/phantasmal_gardeners_retract";

    private static readonly MethodInfo SetHasGainedBlockThisTurn =
        AccessTools.PropertySetter(
            typeof(SkittishPower),
            nameof(SkittishPower.HasGainedBlockThisTurn));

    [HarmonyPrefix]
    private static bool BeforeAfterAttack(
        SkittishPower __instance,
        PlayerChoiceContext choiceContext,
        AttackCommand command,
        ref Task __result)
    {
        if (__instance.Owner?.HasPower<PhantomPower>() != true)
        {
            return true;
        }

        __result = ResolvePhantomSkittish(
            __instance,
            command);
        return false;
    }

    private static async Task ResolvePhantomSkittish(
        SkittishPower power,
        AttackCommand command)
    {
        if (power.HasGainedBlockThisTurn ||
            !command.DamageProps.HasFlag(ValueProp.Move))
        {
            return;
        }

        DamageResult damageResult = command.Results
            .SelectMany((List<DamageResult> results) => results)
            .FirstOrDefault(result => result.Receiver == power.Owner);
        if (damageResult == null || damageResult.UnblockedDamage == 0)
        {
            return;
        }

        SetHasGainedBlockThisTurn.Invoke(power, new object[] { true });
        SfxCmd.Play(RetractSfx);
        await CreatureCmd.TriggerAnim(power.Owner, "BlockStart", 0.3f);
        await CreatureCmd.GainBlock(
            power.Owner,
            power.Amount,
            ValueProp.Unpowered,
            null);
    }
}
