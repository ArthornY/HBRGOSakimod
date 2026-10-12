using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomCeremonialBeastHelper
{
    private static readonly Func<CeremonialBeast, int> PlowAmount =
        AccessTools.MethodDelegate<Func<CeremonialBeast, int>>(
            AccessTools.PropertyGetter(typeof(CeremonialBeast), "PlowAmount"));
    private static readonly Func<CeremonialBeast, int> PlowStrength =
        AccessTools.MethodDelegate<Func<CeremonialBeast, int>>(
            AccessTools.PropertyGetter(typeof(CeremonialBeast), "PlowStrength"));
    private static readonly Func<CeremonialBeast, int> CrushStrength =
        AccessTools.MethodDelegate<Func<CeremonialBeast, int>>(
            AccessTools.PropertyGetter(typeof(CeremonialBeast), "CrushStrength"));

    internal static bool IsPhantomBeast(Creature creature) =>
        creature?.Monster is CeremonialBeast && creature.PetOwner != null &&
        creature.HasPower<PhantomPower>();

    internal static int GetPlowThreshold(Creature phantom) =>
        (int)Math.Min(BigCombatNumbers.NativeStatCap, GetRealPlowThreshold(phantom));

    internal static decimal GetRealPlowThreshold(Creature phantom)
    {
        CeremonialBeast beast = (CeremonialBeast)phantom.Monster;
        using IDisposable scope = PhantomAscensionHelper.BeginPhantomEvaluation();
        // Preserve the native HP ratio after boss scaling, levels, fusion and HP changes.
        return Math.Max(1m, decimal.Floor(
            LargeCreatureHealthHelper.GetMaxHp(phantom) * PlowAmount(beast) / Math.Max(1, beast.MaxInitialHp)));
    }

    internal static void RefreshPlowThreshold(Creature phantom)
    {
        if (IsPhantomBeast(phantom) && phantom.GetPower<PlowPower>() is { } plow)
            plow.SetAmount(GetPlowThreshold(phantom));
    }

    internal static async Task AfterAttack(Creature phantom, MoveState move)
    {
        if (phantom.Monster is not CeremonialBeast beast) return;

        using IDisposable scope = PhantomAscensionHelper.BeginPhantomEvaluation();
        int strength = move.Id switch
        {
            "PLOW_MOVE" => PlowStrength(beast),
            "CRUSH_MOVE" => CrushStrength(beast),
            _ => 0
        };
        if (strength <= 0) return;

        // Attack intents replay damage separately; retain the native post-attack buff once per move.
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(),
            phantom, strength, phantom, null);
    }
}
