using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

public static class HoverAuditProgram
{
    private static readonly Dictionary<ushort, OpCode> Ops = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode))
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => unchecked((ushort)o.Value));

    public static void Main()
    {
        var asm = Assembly.LoadFrom("../../libs/sts2.dll");
        foreach (var type in asm.GetTypes().Where(t =>
                     t.FullName?.Contains("HoverTip") == true &&
                     (t.FullName.Contains("HoverTips.HoverTip") ||
                      t.FullName.Contains("Nodes.HoverTips.NHoverTip"))))
        {
            Console.WriteLine($"\nTYPE {type.FullName}");
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                Console.WriteLine($"  {member.MemberType}: {member}");
        }

        foreach (var typeName in new[] {
                     "MegaCrit.Sts2.Core.HoverTips.HoverTip",
                     "MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet",
                     "MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipCardContainer" })
        {
            var type = asm.GetType(typeName);
            if (type == null) continue;
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                Console.WriteLine($"\nIL {type.FullName}.{method.Name}");
                DumpIl(method);
            }
        }
    }

    private static void DumpIl(MethodInfo method)
    {
        var body = method.GetMethodBody();
        if (body == null) return;
        var il = body.GetILAsByteArray()!;
        int i = 0;
        while (i < il.Length)
        {
            int offset = i;
            ushort code = il[i++];
            if (code == 0xfe) code = (ushort)(0xfe00 | il[i++]);
            var op = Ops[code];
            object? operand = null;
            int size = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 0
            };
            if (size > 0)
            {
                if (op.OperandType is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineTok or OperandType.InlineType or OperandType.InlineString)
                {
                    int token = BitConverter.ToInt32(il, i);
                    try
                    {
                        operand = op.OperandType == OperandType.InlineString
                            ? method.Module.ResolveString(token)
                            : method.Module.ResolveMember(token, method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments());
                    }
                    catch { operand = $"token 0x{token:x8}"; }
                }
                else if (size == 1) operand = il[i];
                else if (size == 2) operand = BitConverter.ToUInt16(il, i);
                else if (size == 4) operand = BitConverter.ToInt32(il, i);
                else if (size == 8) operand = BitConverter.ToInt64(il, i);
                else operand = $"{size} bytes";
            }
            i += size;
            Console.WriteLine($"  {offset:x4}: {op.Name} {operand}");
        }
    }
}
