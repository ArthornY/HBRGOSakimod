using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>Discovers HBR powers and the original-game powers actually referenced by HBR code.</summary>
internal static class HbrManualGlossaryDiscovery
{
    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null))
        .ToDictionary(opcode => opcode.Value);
    private static readonly Lazy<IReadOnlyList<Type>> PowerTypes = new(() =>
        DiscoverPowerTypes(typeof(HbrManualGlossaryDiscovery).Assembly));

    internal static IReadOnlyList<Type> ReferencedPowerTypes => PowerTypes.Value;

    internal static IReadOnlyList<Type> DiscoverPowerTypes(Assembly source)
    {
        HashSet<Type> powers = new();
        Assembly game = typeof(PowerModel).Assembly;
        void IncludeType(Type type)
        {
            if (type == null) return;
            if (type.HasElementType) IncludeType(type.GetElementType());
            if (type.IsGenericType)
                foreach (Type argument in type.GetGenericArguments()) IncludeType(argument);
            if (!type.IsAbstract && !type.ContainsGenericParameters &&
                (type.Assembly == source || type.Assembly == game) &&
                typeof(PowerModel).IsAssignableFrom(type)) powers.Add(type);
        }
        void IncludeMember(MemberInfo member)
        {
            if (member is Type type) { IncludeType(type); return; }
            IncludeType(member?.DeclaringType);
            switch (member)
            {
                case MethodInfo method:
                    IncludeType(method.ReturnType);
                    foreach (Type argument in method.GetGenericArguments()) IncludeType(argument);
                    foreach (ParameterInfo parameter in method.GetParameters()) IncludeType(parameter.ParameterType);
                    break;
                case ConstructorInfo constructor:
                    foreach (ParameterInfo parameter in constructor.GetParameters()) IncludeType(parameter.ParameterType);
                    break;
                case FieldInfo field:
                    IncludeType(field.FieldType);
                    break;
            }
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (Type type in source.GetTypes())
        {
            IncludeType(type);
            foreach (FieldInfo field in type.GetFields(flags)) IncludeMember(field);
            foreach (MethodBase method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)))
            {
                IncludeMember(method);
                byte[] il = method.GetMethodBody()?.GetILAsByteArray();
                if (il == null) continue;
                Type[] typeArguments = type.IsGenericType ? type.GetGenericArguments() : null;
                Type[] methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
                for (int position = 0; position < il.Length;)
                {
                    short opcodeValue = il[position++];
                    if (opcodeValue == 0xfe)
                    {
                        if (position >= il.Length) break;
                        opcodeValue = unchecked((short)(0xfe00 | il[position++]));
                    }
                    if (!OpCodesByValue.TryGetValue(opcodeValue, out OpCode opcode)) break;
                    int length = OperandLength(opcode.OperandType, il, position);
                    if (length < 0 || position + (long)length > il.Length) break;
                    if (opcode.OperandType is OperandType.InlineType or OperandType.InlineMethod or
                        OperandType.InlineTok or OperandType.InlineField)
                    {
                        int token = BitConverter.ToInt32(il, position);
                        try { IncludeMember(method.Module.ResolveMember(token, typeArguments, methodArguments)); }
                        // A compatibility branch can name an API absent in this game version.
                        // It must not prevent unrelated, resolvable reference entries from opening.
                        catch (ArgumentException) { }
                        catch (TypeLoadException) { }
                        catch (MissingMemberException) { }
                        catch (FileNotFoundException) { }
                    }
                    position += length;
                }
            }
        }
        return Array.AsReadOnly(powers.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray());
    }

    private static int OperandLength(OperandType kind, byte[] il, int position) => kind switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => position + 4 <= il.Length &&
            BitConverter.ToInt32(il, position) is >= 0 and <= (int.MaxValue - 4) / 4
                ? 4 + BitConverter.ToInt32(il, position) * 4 : -1,
        _ => 4
    };
}
