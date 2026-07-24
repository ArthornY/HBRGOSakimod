using System;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

static void Dump(Type type)
{
    Console.WriteLine($"TYPE {type.FullName}");
    foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                 .Where(method => method.DeclaringType == type)
                 .OrderBy(method => method.Name))
    {
        Console.WriteLine(method.Name);
    }

    foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                 .Where(property => property.DeclaringType == type)
                 .OrderBy(property => property.Name))
    {
        Console.WriteLine($"PROPERTY {property.Name}");
    }
}

static void DumpHooks(Type type)
{
    Console.WriteLine($"HOOKS {type.FullName}");
    foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                 .Where(method => method.IsVirtual && (
                     method.Name.Contains("Power") ||
                     method.Name.Contains("Damage") ||
                     method.Name.Contains("Card")))
                 .OrderBy(method => method.Name))
    {
        Console.WriteLine($"{method.ReturnType.FullName} {method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.FullName + " " + parameter.Name))})");
    }
}

static void DumpSignatures(Type type)
{
    Console.WriteLine($"SIGNATURES {type.FullName}");
    foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                 .Where(method => method.DeclaringType == type)
                 .OrderBy(method => method.Name))
    {
        Console.WriteLine($"{method.ReturnType.FullName} {method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.FullName + " " + parameter.Name))})");
    }
}

Dump(typeof(LocString));
foreach (MethodInfo method in typeof(LocString).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
             .Where(method => method.Name is "Add" or "AddObj" or "AddVariablesFrom" or "GetFormattedText")
             .OrderBy(method => method.Name))
{
    Console.WriteLine(
        $"LOC_STRING_METHOD {method.Attributes} {method.ReturnType.FullName} {method.Name}(" +
        $"{string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.FullName + " " + parameter.Name))})");
}
foreach (MethodInfo method in typeof(CardModel).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
             .Where(method => method.Name.Contains("Description"))
             .OrderBy(method => method.Name))
{
    Console.WriteLine(
        $"CARD_DESCRIPTION_METHOD {method.Attributes} {method.ReturnType.FullName} {method.Name}(" +
        $"{string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.FullName + " " + parameter.Name))})");
}
