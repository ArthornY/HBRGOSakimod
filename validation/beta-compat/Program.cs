using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

return Audit.Run(args);

static class Audit
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static readonly HashSet<string> PatchNames = ["Prefix", "Postfix", "Transpiler", "Finalizer", "ReversePatch"];
    static readonly List<object> Issues = [];
    static readonly List<object> Reviews = [];
    static readonly List<object> References = [];
    static readonly List<PatchClass> Classes = [];
    static readonly Dictionary<string, object> Report = new();
    static string reportPath = "";
    static int parameterChecks;

    internal static int Run(string[] args)
    {
        try
        {
            var options = Parse(args);
            string modPath = RequiredPath(options, "mod");
            string sourceGamePath = RequiredPath(options, "game");
            string dependencies = RequiredPath(options, "dependencies", directory: true);
            string runtime = RequiredPath(options, "runtime", directory: true);
            string godot = RequiredPath(options, "godot");
            if (options.GetValueOrDefault("expected-mod-sha256") is { } expectedHash && !Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(modPath))).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Candidate SHA256 does not match --expected-mod-sha256.");
            reportPath = Path.GetFullPath(options.GetValueOrDefault("report") ?? throw new ArgumentException("Missing --report"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            string containerPath = modPath;
            if (options.GetValueOrDefault("embedded-resource") is { } resourceName) modPath = ExtractResource(containerPath, resourceName);
            string inputAssemblyName = AssemblyName.GetAssemblyName(modPath).Name!;
            string loadedGamePath = options.ContainsKey("isolate-telemetry") ? IsolateTelemetry(sourceGamePath) : sourceGamePath;
            var roots = new[] { dependencies, Path.GetDirectoryName(loadedGamePath)!, Path.GetDirectoryName(modPath)!, Path.GetDirectoryName(containerPath)!, Path.GetDirectoryName(godot)!, runtime }
                .Concat((options.GetValueOrDefault("extra-probe") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Path.GetFullPath)).Distinct().ToArray();
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                if (name.Name == "sts2") return context.LoadFromAssemblyPath(loadedGamePath);
                if (name.Name == inputAssemblyName) return context.LoadFromAssemblyPath(modPath);
                if (name.Name == "GodotSharp") return context.LoadFromAssemblyPath(godot);
                foreach (string root in roots)
                {
                    string path = Path.Combine(root, name.Name + ".dll");
                    if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
                }
                return null;
            };
            Report["startedUtc"] = DateTimeOffset.UtcNow;
            Report["runtime"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            Report["processArchitecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
            Report["boundaries"] = new[] { "Managed assembly/reflection/Harmony only; no game boot, native Godot, saves, network, package, or telemetry initializer.", "Target resolution and registration do not establish complete gameplay/transpiler correctness." };
            Report["inputs"] = new Dictionary<string, object>
            {
                ["mod"] = Identity(modPath), ["sourceGame"] = Identity(sourceGamePath), ["loadedGame"] = Identity(loadedGamePath),
                ["godot"] = Identity(godot), ["dependencyDirectory"] = dependencies, ["runtimeDirectory"] = runtime,
                ["dependencies"] = new[] { "0Harmony.dll", "BaseLib.dll", "STS2-RitsuLib.dll" }.Select(name => Identity(Path.Combine(dependencies, name))).ToArray()
            };
            var harmony = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dependencies, "0Harmony.dll"));
            var game = AssemblyLoadContext.Default.LoadFromAssemblyPath(loadedGamePath);
            var mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(modPath);
            Report["gameHooks"] = ReadHooks(game);
            Type[] types;
            try { types = mod.GetTypes(); }
            catch (ReflectionTypeLoadException error)
            {
                types = error.Types.OfType<Type>().ToArray();
                foreach (var loader in error.LoaderExceptions.Where(item => item != null).DistinctBy(item => item!.ToString()))
                    Issue("type-load", mod.FullName!, loader!);
            }
            Report["loadedModTypeCount"] = types.Length;
            CheckReferences(modPath, mod.ManifestModule, types);
            ResolvePatches(harmony, types);
            Save();
            if (options.ContainsKey("install")) Install(harmony, options.GetValueOrDefault("patch-filter"));
            Report["loadedAssemblies"] = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => new { a.FullName, a.Location }).OrderBy(a => a.FullName).ToArray();
            Report["completedUtc"] = DateTimeOffset.UtcNow;
            Save();
            Console.WriteLine($"{Classes.Count} Harmony classes; {Classes.Sum(c => c.Targets.Count)} patch-target pairs; {parameterChecks} parameter checks; {References.Count} metadata references; {Issues.Count} issues; {Reviews.Count} reviews.");
            foreach (object item in Issues) Console.WriteLine(JsonSerializer.Serialize(item));
            Console.WriteLine(reportPath);
            return Issues.Count == 0 ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(Root(error));
            if (!string.IsNullOrEmpty(reportPath)) { Issue("tool-error", "audit", error); Save(); }
            return 2;
        }
    }

    static Dictionary<string, string?> Parse(string[] args)
    {
        var result = new Dictionary<string, string?>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) throw new ArgumentException("Unexpected argument: " + args[i]);
            string key = args[i][2..];
            result.Add(key, key is "install" or "isolate-telemetry" ? null : ++i < args.Length ? args[i] : throw new ArgumentException("Missing value for " + key));
        }
        return result;
    }

    static string RequiredPath(Dictionary<string, string?> options, string name, bool directory = false)
    {
        string path = Path.GetFullPath(options.GetValueOrDefault(name) ?? throw new ArgumentException("Missing --" + name));
        if (directory ? !Directory.Exists(path) : !File.Exists(path)) throw new FileNotFoundException("Missing --" + name, path);
        return path;
    }

    static object Identity(string path) => new
    {
        path, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), length = new FileInfo(path).Length,
        assemblyName = AssemblyName.GetAssemblyName(path).FullName,
        fileVersion = FileVersionInfo.GetVersionInfo(path).FileVersion,
        productVersion = FileVersionInfo.GetVersionInfo(path).ProductVersion
    };

    static string ExtractResource(string container, string resourceName)
    {
        using var pe = new PEReader(File.OpenRead(container));
        var metadata = pe.GetMetadataReader();
        var resource = metadata.ManifestResources.Select(metadata.GetManifestResource).Single(r => metadata.GetString(r.Name) == resourceName);
        if (!resource.Implementation.IsNil) throw new InvalidOperationException("Resource is external rather than embedded.");
        var reader = pe.GetSectionData(checked(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress + (int)resource.Offset)).GetReader();
        byte[] data = reader.ReadBytes(reader.ReadInt32());
        string fileName = Path.GetFileName(resourceName);
        if (fileName != resourceName) throw new InvalidOperationException("Resource name must be a file name without directories.");
        string path = Path.Combine(Path.GetDirectoryName(reportPath)!, "extracted", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
        Report["embeddedResource"] = new { container = Identity(container), resourceName, extracted = Identity(path) };
        return path;
    }

    static string IsolateTelemetry(string sourcePath)
    {
        byte[] source = File.ReadAllBytes(sourcePath);
        using var pe = new PEReader(new MemoryStream(source, writable: false));
        var metadata = pe.GetMetadataReader();
        var module = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(t => metadata.GetString(t.Name) == "<Module>");
        var initializer = metadata.GetMethodDefinition(module.GetMethods().Single(h => metadata.GetString(metadata.GetMethodDefinition(h).Name) == ".cctor"));
        byte[] il = pe.GetMethodBody(initializer.RelativeVirtualAddress).GetILBytes()!;
        if (il.Length != 6 || il[0] != 0x28 || il[5] != 0x2a) throw new InvalidOperationException("Isolation refused: initializer must be exactly call + ret.");
        var target = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, 1));
        if (target.Kind != HandleKind.MethodDefinition) throw new InvalidOperationException("Isolation refused: call must be a local method definition.");
        var method = metadata.GetMethodDefinition((MethodDefinitionHandle)target);
        var owner = metadata.GetTypeDefinition(method.GetDeclaringType());
        if (metadata.GetString(method.Name) != "Init" || metadata.GetString(owner.Name) != "SentryAutoInit")
            throw new InvalidOperationException("Isolation refused: initializer is not SentryAutoInit.Init.");
        var section = pe.PEHeaders.SectionHeaders.Single(s => initializer.RelativeVirtualAddress >= s.VirtualAddress && initializer.RelativeVirtualAddress < s.VirtualAddress + Math.Max(s.VirtualSize, s.SizeOfRawData));
        int body = section.PointerToRawData + initializer.RelativeVirtualAddress - section.VirtualAddress;
        int header = (source[body] & 3) == 2 ? 1 : (BitConverter.ToUInt16(source, body) >> 12) * 4;
        int offset = body + header;
        if (!source.AsSpan(offset, il.Length).SequenceEqual(il)) throw new InvalidOperationException("Isolation refused: incorrect PE body offset.");
        byte[] copy = (byte[])source.Clone();
        Array.Clear(copy, offset, 5);
        if (!source.AsSpan(0, offset).SequenceEqual(copy.AsSpan(0, offset)) || !source.AsSpan(offset + 6).SequenceEqual(copy.AsSpan(offset + 6)))
            throw new InvalidOperationException("Isolation modified bytes outside initializer.");
        string directory = Path.Combine(Path.GetDirectoryName(reportPath)!, "telemetry-isolated");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "sts2.dll");
        if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Refusing to overwrite input reference DLL.");
        File.WriteAllBytes(path, copy);
        Report["telemetryIsolation"] = new { sourcePath, copyPath = path, ilOffset = offset, originalIl = Convert.ToHexString(il), replacementIl = Convert.ToHexString(copy.AsSpan(offset, 6)), outsideBytesIdentical = true };
        return path;
    }

    static object[] ReadHooks(Assembly game)
    {
        string[] names = ["MegaCrit.Sts2.Core.Models.Powers.DoomPower", "MegaCrit.Sts2.Core.Models.AbstractModel", "MegaCrit.Sts2.Core.Commands.PowerCmd", "MegaCrit.Sts2.Core.Commands.CardCmd", "MegaCrit.Sts2.Core.Combat.CombatManager"];
        return names.Select(name =>
        {
            Type? type = game.GetType(name);
            return (object)new { type = name, exists = type != null, methods = type?.GetMethods(All | BindingFlags.DeclaredOnly)
                .Where(m => name.EndsWith("DoomPower") || m.Name.Contains("ModifyDamage") || m.Name.Contains("StartCombat") || m.Name is "Apply" or "ModifyAmount" or "Remove" or "Exhaust")
                .Select(Signature).Order().ToArray() ?? [] };
        }).ToArray();
    }

    static void CheckReferences(string modPath, Module module, Type[] types)
    {
        using var pe = new PEReader(File.OpenRead(modPath));
        var metadata = pe.GetMetadataReader();
        var contexts = GenericTokenContexts(types);
        foreach (var handle in metadata.TypeReferences.Select(h => (EntityHandle)h).Concat(metadata.MemberReferences.Select(h => (EntityHandle)h)))
        {
            int token = MetadataTokens.GetToken(handle);
            string kind = handle.Kind.ToString();
            string name = handle.Kind == HandleKind.TypeReference ? metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)handle).Name) : metadata.GetString(metadata.GetMemberReference((MemberReferenceHandle)handle).Name);
            try
            {
                MemberInfo? resolved;
                try { resolved = module.ResolveMember(token); }
                catch (ArgumentException error) when (error.Message.Contains("lack of a generic context", StringComparison.OrdinalIgnoreCase))
                {
                    resolved = null;
                    if (!contexts.TryGetValue(token, out var candidates)) throw;
                    Exception? last = null;
                    foreach (var context in candidates)
                    {
                        try { resolved = module.ResolveMember(token, context.Types, context.Methods); break; }
                        catch (Exception failure) { last = failure; }
                    }
                    if (resolved == null) throw new ArgumentException("Generic context could not resolve token: " + last?.Message, last);
                }
                References.Add(new { token = $"0x{token:X8}", kind, name, resolved = resolved?.ToString(), owner = resolved?.DeclaringType?.FullName, ok = resolved != null });
                if (resolved == null) Issue("unresolved-reference", name + $" (0x{token:X8})", new MissingMemberException());
            }
            catch (ArgumentException error) when (error.Message.Contains("generic context", StringComparison.OrdinalIgnoreCase))
            {
                References.Add(new { token = $"0x{token:X8}", kind, name, genericContextRequired = true });
                Reviews.Add(new { category = "generic-context", name, token = $"0x{token:X8}", error = error.Message });
            }
            catch (Exception error) { Issue("unresolved-reference", name + $" (0x{token:X8})", error); }
        }
    }

    static Dictionary<int, List<GenericContext>> GenericTokenContexts(Type[] types)
    {
        var result = new Dictionary<int, List<GenericContext>>();
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (OpCode)field.GetValue(null)!).ToDictionary(opcode => unchecked((ushort)opcode.Value));
        foreach (Type type in types)
        foreach (MethodBase method in type.GetMethods(All | BindingFlags.DeclaredOnly).Cast<MethodBase>().Concat(type.GetConstructors(All | BindingFlags.DeclaredOnly)))
        {
            if (!type.ContainsGenericParameters && !method.ContainsGenericParameters) continue;
            byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null) continue;
            var context = new GenericContext(type.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : []);
            int offset = 0;
            while (offset < il.Length)
            {
                ushort code = il[offset++];
                if (code == 0xfe) code = (ushort)(0xfe00 | il[offset++]);
                OperandType operand = opcodes[code].OperandType;
                if (operand is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok)
                {
                    int token = BitConverter.ToInt32(il, offset);
                    if (!result.TryGetValue(token, out var list)) result[token] = list = [];
                    list.Add(context);
                }
                offset += operand switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + BitConverter.ToInt32(il, offset) * 4,
                    _ => 4
                };
            }
        }
        return result;
    }

    sealed record GenericContext(Type[] Types, Type[] Methods);

    static void ResolvePatches(Assembly harmony, Type[] types)
    {
        Type methods = harmony.GetType("HarmonyLib.HarmonyMethodExtensions", true)!;
        Type patchTools = harmony.GetType("HarmonyLib.PatchTools", true)!;
        Type harmonyMethod = harmony.GetType("HarmonyLib.HarmonyMethod", true)!;
        foreach (Type type in types.Where(t => Has(t, "HarmonyPatch")).OrderBy(t => t.FullName))
        {
            var entry = new PatchClass { Name = type.FullName!, RuntimeType = type };
            Classes.Add(entry);
            try
            {
                MethodInfo[] declared = type.GetMethods(All | BindingFlags.DeclaredOnly);
                MethodInfo? prepare = declared.SingleOrDefault(m => m.Name == "Prepare" || Has(m, "HarmonyPrepare"));
                if (prepare != null)
                {
                    entry.PrepareMethod = Signature(prepare);
                    entry.Prepared = (bool)prepare.Invoke(null, FactoryArguments(prepare, harmony))!;
                    if (entry.Prepared == false) { entry.Installation = "skipped-by-prepare"; continue; }
                }
                var patches = declared.Where(m => PatchNames.Contains(m.Name) || m.CustomAttributes.Any(a => PatchNames.Any(n => a.AttributeType.Name == "Harmony" + n))).ToArray();
                entry.PatchMethods = patches.Select(Signature).ToArray();
                var many = declared.SingleOrDefault(m => m.Name == "TargetMethods" || Has(m, "HarmonyTargetMethods"));
                var one = declared.SingleOrDefault(m => m.Name == "TargetMethod" || Has(m, "HarmonyTargetMethod"));
                List<MethodBase?> targets = [];
                entry.Dynamic = many != null || one != null;
                if (many != null) targets.AddRange(((IEnumerable)many.Invoke(null, FactoryArguments(many, harmony))!).Cast<MethodBase?>());
                else if (one != null) targets.Add(one.Invoke(null, FactoryArguments(one, harmony)) as MethodBase);
                if (entry.Dynamic && targets.Count == 0) entry.EmptyDynamicTargets = true;
                object? classInfo = InvokeStatic(methods, "GetMergedFromType", type);
                foreach (MethodInfo patch in patches)
                {
                    List<MethodBase?> selected = targets;
                    if (!entry.Dynamic)
                    {
                        object? patchInfo = InvokeStatic(methods, "GetMergedFromMethod", patch);
                        object? merged = InvokeStatic(methods, "Merge", classInfo, patchInfo);
                        FieldInfo methodType = harmonyMethod.GetField("methodType")!;
                        if (methodType.GetValue(merged) == null) methodType.SetValue(merged, Enum.Parse(harmony.GetType("HarmonyLib.MethodType")!, "Normal"));
                        selected = [InvokeStatic(patchTools, "GetOriginalMethod", merged) as MethodBase];
                    }
                    foreach (MethodBase? target in selected)
                    {
                        if (target == null) { Issue("missing-target", type.FullName + "." + patch.Name, new MissingMethodException()); continue; }
                        var resolved = new PatchTarget { Patch = Signature(patch), Original = Signature(target), RuntimeMethod = target };
                        entry.Targets.Add(resolved);
                        CheckParameters(patch, target, resolved);
                    }
                }
            }
            catch (Exception error) { entry.Error = Root(error).ToString(); Issue("target-resolution", type.FullName!, error); }
        }
    }

    static object?[] FactoryArguments(MethodInfo factory, Assembly harmony) => factory.GetParameters().Select(p => p.ParameterType.FullName == "HarmonyLib.Harmony" ? Activator.CreateInstance(harmony.GetType("HarmonyLib.Harmony")!, "hbrgo.compat.target-discovery") : p.HasDefaultValue ? p.DefaultValue : throw new InvalidOperationException("Unsupported target factory parameter: " + p)).ToArray();

    static void CheckParameters(MethodInfo patch, MethodBase original, PatchTarget record)
    {
        if (Has(patch, "HarmonyTranspiler") || patch.Name == "Transpiler" || Has(patch, "HarmonyReversePatch")) return;
        ParameterInfo[] args = original.GetParameters();
        foreach (ParameterInfo argument in patch.GetParameters())
        {
            string name = argument.Name!;
            Type? expected = null;
            var renamed = argument.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "HarmonyArgument");
            if (renamed?.ConstructorArguments.FirstOrDefault().Value is int position) expected = args.ElementAtOrDefault(position)?.ParameterType;
            else if (renamed?.ConstructorArguments.FirstOrDefault().Value is string targetName) expected = args.FirstOrDefault(a => a.Name == targetName)?.ParameterType;
            else if (name == "__instance") expected = original.IsStatic ? null : original.DeclaringType;
            else if (name == "__result") expected = (original as MethodInfo)?.ReturnType;
            else if (name.StartsWith("___"))
                for (Type? owner = original.DeclaringType; owner != null && expected == null; owner = owner.BaseType) expected = owner.GetField(name[3..], All)?.FieldType;
            else if (name.StartsWith("__") && int.TryParse(name[2..], out int index)) expected = args.ElementAtOrDefault(index)?.ParameterType;
            else if (name.StartsWith("__")) { record.SpecialParameters.Add(name); continue; }
            else expected = args.FirstOrDefault(a => a.Name == name)?.ParameterType;
            parameterChecks++;
            Type requested = Plain(argument.ParameterType);
            bool ok = expected != null && (requested.IsAssignableFrom(Plain(expected)) || Plain(expected).IsAssignableFrom(requested));
            record.Parameters.Add(new { name, requested = argument.ParameterType.ToString(), original = expected?.ToString(), ok });
            if (!ok) Issue("parameter", Signature(patch) + " -> " + Signature(original) + ": " + name, new InvalidOperationException($"Patch parameter {argument.ParameterType} cannot bind {expected?.ToString() ?? "missing original parameter"}."));
        }
    }

    static void Install(Assembly harmonyAssembly, string? filter)
    {
        Type harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true)!;
        string id = "hbrgo.compat.audit." + Guid.NewGuid().ToString("N");
        object instance = Activator.CreateInstance(harmonyType, id)!;
        Report["installationOwner"] = id;
        foreach (PatchClass entry in Classes.Where(c => c.Error == null && c.Prepared != false && (filter == null || Regex.IsMatch(c.Name, filter))))
        {
            Report["currentlyInstalling"] = entry.Name;
            Save();
            try
            {
                object processor = harmonyType.GetMethod("CreateClassProcessor", [typeof(Type)])!.Invoke(instance, [entry.RuntimeType])!;
                object? result = processor.GetType().GetMethod("Patch", Type.EmptyTypes)!.Invoke(processor, null);
                entry.InstalledMethods = result is IEnumerable enumerable ? enumerable.Cast<MethodBase>().Select(Signature).ToArray() : [];
                var registered = new List<object>();
                foreach (MethodBase target in entry.Targets.Select(t => t.RuntimeMethod!).Distinct())
                {
                    object? info = harmonyType.GetMethod("GetPatchInfo", All)!.Invoke(null, [target]);
                    foreach (string patchKind in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers" })
                    {
                        object? patches = info?.GetType().GetField(patchKind)?.GetValue(info) ?? info?.GetType().GetProperty(patchKind)?.GetValue(info);
                        if (patches is not IEnumerable list) continue;
                        foreach (object patch in list)
                        {
                            string? owner = patch.GetType().GetField("owner")?.GetValue(patch) as string;
                            var method = patch.GetType().GetProperty("PatchMethod")?.GetValue(patch) as MethodBase;
                            if (owner == id && method?.DeclaringType == entry.RuntimeType) registered.Add(new { original = Signature(target), kind = patchKind, patch = Signature(method) });
                        }
                    }
                }
                entry.Registered = registered;
                entry.Installation = entry.InstalledMethods.Length == 0 ? "skipped-by-processor" : registered.Count > 0 ? "registered" : "registration-missing";
                if (entry.Installation == "registration-missing") Issue("registration", entry.Name, new InvalidOperationException("Harmony returned patched methods but production patch was absent from registry."));
            }
            catch (Exception error)
            {
                entry.Installation = "failed";
                entry.InstallationError = Root(error).ToString();
                Issue("installation", entry.Name, error);
            }
            Save();
        }
        Report.Remove("currentlyInstalling");
    }

    static object? InvokeStatic(Type type, string name, params object?[] args) => type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
    static bool Has(MemberInfo member, string attribute) => member.CustomAttributes.Any(a => a.AttributeType.Name == attribute);
    static Type Plain(Type type) => type.IsByRef ? type.GetElementType()! : type;
    static string Signature(MethodBase method) => (method.DeclaringType?.FullName ?? "<global>") + "." + method;
    static Exception Root(Exception error) { while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException; return error; }
    static void Issue(string category, string subject, Exception error) => Issues.Add(new { category, subject, error = Root(error).ToString() });
    static void Save()
    {
        Report["ok"] = Issues.Count == 0;
        Report["issues"] = Issues;
        Report["reviews"] = Reviews;
        Report["references"] = References;
        Report["patchClasses"] = Classes;
        Report["counts"] = new { classes = Classes.Count, targets = Classes.Sum(c => c.Targets.Count), parameters = parameterChecks, references = References.Count, issues = Issues.Count, reviews = Reviews.Count, installedClasses = Classes.Count(c => c.Installation == "registered"), skippedClasses = Classes.Count(c => c.Installation is "skipped-by-processor" or "skipped-by-prepare") };
        File.WriteAllText(reportPath, JsonSerializer.Serialize(Report, Json));
    }

    sealed class PatchClass
    {
        public string Name { get; set; } = "";
        [System.Text.Json.Serialization.JsonIgnore] public Type RuntimeType { get; set; } = null!;
        public string[] PatchMethods { get; set; } = [];
        public bool Dynamic { get; set; }
        public bool EmptyDynamicTargets { get; set; }
        public string? PrepareMethod { get; set; }
        public bool? Prepared { get; set; }
        public List<PatchTarget> Targets { get; set; } = [];
        public string? Error { get; set; }
        public string? Installation { get; set; }
        public string? InstallationError { get; set; }
        public string[] InstalledMethods { get; set; } = [];
        public List<object> Registered { get; set; } = [];
    }

    sealed class PatchTarget
    {
        public string Patch { get; set; } = "";
        public string Original { get; set; } = "";
        public List<object> Parameters { get; set; } = [];
        public List<string> SpecialParameters { get; set; } = [];
        [System.Text.Json.Serialization.JsonIgnore] public MethodBase? RuntimeMethod { get; set; }
    }
}
