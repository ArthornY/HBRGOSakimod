using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

if (args.Length != 1)
{
    Console.Error.WriteLine("Expected the path to HBRGOSakiMod.dll.");
    return 1;
}

Assembly modAssembly = Assembly.LoadFrom(args[0]);
Harmony harmony = new("hbrgosakimod.patch-smoke");
string[] patchTypes =
[
    "HBRGOSakiMod.Core.Patches.NCardLibraryGridUpdateGridNavigationPatch",
    "HBRGOSakiMod.Core.Patches.Combat.FeintDamageNegationPatch",
];

foreach (string patchTypeName in patchTypes)
{
    Type patchType = modAssembly.GetType(patchTypeName, throwOnError: true);
    MethodBase[] patchedMethods = harmony.CreateClassProcessor(patchType)
        .Patch()
        .ToArray();
    Console.WriteLine(
        $"{patchType.Name}: PATCH_OK {string.Join(", ", patchedMethods.Select(method => $"{method.DeclaringType?.FullName}.{method.Name}"))}");
}

Type feintPowerType = modAssembly.GetType(
    "HBRGOSakiMod.Core.Models.Powers.FeintMarkPower",
    throwOnError: true);
MethodInfo eligibilityMethod = feintPowerType.GetMethod(
    "IsDodgeNegationEligible",
    BindingFlags.Static | BindingFlags.NonPublic)
    ?? throw new MissingMethodException(feintPowerType.FullName, "IsDodgeNegationEligible");
Type valuePropType = eligibilityMethod.GetParameters()[2].ParameterType;
object noProps = Enum.ToObject(valuePropType, 0);
object unblockable = Enum.Parse(valuePropType, "Unblockable");

AssertEligibility(7m, 8, noProps, expected: true);
AssertEligibility(8m, 8, noProps, expected: false);
AssertEligibility(7m, 8, unblockable, expected: false);
Console.WriteLine("FeintMarkPower: ELIGIBILITY_OK");

return 0;

void AssertEligibility(decimal damage, int dodge, object props, bool expected)
{
    bool actual = (bool)eligibilityMethod.Invoke(null, [damage, dodge, props]);
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"Unexpected feint eligibility for damage={damage}, dodge={dodge}, props={props}: {actual}");
    }
}
