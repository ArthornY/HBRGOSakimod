using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Compatibility;

internal static class RngCompatibility
{
    // The native Shuffle method disappeared in v0.113. Keep its exact
    // Fisher-Yates order and consume the supplied stream once per swap.
    internal static void ShuffleCompat<T>(this Rng rng, IList<T> list)
    {
        for (int index = list.Count - 1; index > 0; index--)
        {
            int other = rng.NextInt(index + 1);
            (list[index], list[other]) = (list[other], list[index]);
        }
    }

    private static readonly ConstructorInfo SeedConstructor =
        typeof(Rng).GetConstructor([typeof(uint), typeof(int)]) ??
        typeof(Rng).GetConstructor([typeof(ulong)]) ??
        throw new MissingMethodException(typeof(Rng).FullName, ".ctor(seed[, counter])");

    private static readonly MethodInfo RunSeedGetter =
        typeof(RunRngSet).GetProperty(nameof(RunRngSet.Seed))?.GetMethod ??
        throw new MissingMemberException(typeof(RunRngSet).FullName, nameof(RunRngSet.Seed));

    private static readonly ConstructorInfo NamedSeedConstructor =
        typeof(Rng).GetConstructor([RunSeedGetter.ReturnType, typeof(string)]) ??
        throw new MissingMethodException(typeof(Rng).FullName, ".ctor(seed, name)");

    private static readonly MethodInfo PlayerSeedGetter =
        typeof(PlayerRngSet).GetProperty(nameof(PlayerRngSet.Seed))?.GetMethod ??
        throw new MissingMemberException(typeof(PlayerRngSet).FullName, nameof(PlayerRngSet.Seed));

    internal static Rng Create(uint seed)
    {
        // The beta widened seeds and removed the optional counter parameter.
        object[] arguments = SeedConstructor.GetParameters().Length == 2
            ? [seed, 0]
            : [(ulong)seed];
        try
        {
            return (Rng)SeedConstructor.Invoke(arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    internal static ulong GetRunSeed(RunRngSet rngs)
    {
        return Convert.ToUInt64(CompatibilityReflection.Invoke(RunSeedGetter, rngs, []));
    }

    // The player's seed is uint on stable and ulong on the beta, like the run's.
    internal static ulong GetPlayerSeed(PlayerRngSet rngs)
    {
        return Convert.ToUInt64(CompatibilityReflection.Invoke(PlayerSeedGetter, rngs, []));
    }

    internal static Rng CreateNamed(RunRngSet rngs, string name)
    {
        // Preserve the native seed width and named-stream derivation in both
        // stable (uint) and beta (ulong), without consuming a shared RNG stream.
        object seed = CompatibilityReflection.Invoke(RunSeedGetter, rngs, []);
        try
        {
            return (Rng)NamedSeedConstructor.Invoke([seed, name]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
