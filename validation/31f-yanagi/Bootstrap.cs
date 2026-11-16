using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class YanagiBootstrap
{
    public static int Main(string[] args)
    {
        try
        {
            string mod=Path.GetFullPath(args[0]), dependencies=Path.GetFullPath(args[2]);
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mod)))!=args[1].ToUpperInvariant())throw new Exception("Candidate hash mismatch");
            string manifestPath=Path.Combine(dependencies,"profile-manifest.json");
            if(File.Exists(manifestPath))
            {
                using var manifest=JsonDocument.Parse(File.ReadAllText(manifestPath));
                foreach(var file in manifest.RootElement.GetProperty("files").EnumerateArray())
                    if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(dependencies,file.GetProperty("file").GetString()!))))!=file.GetProperty("copySha256").GetString())throw new Exception("Runtime profile hash mismatch");
                Environment.SetEnvironmentVariable("HBR_NATIVE_PROFILE",manifest.RootElement.GetProperty("name").GetString());
            }
            string godotDirectory=Path.GetDirectoryName(Path.GetFullPath(args[3]))!;
            string nativePath=Path.Combine(dependencies,"sts2.dll");
            Environment.SetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nativePath))));
            if(args.Length>5&&args[5]=="--isolate-telemetry")nativePath=TelemetryIsolation.Create(nativePath,Path.GetFullPath(args[4]));
            AssemblyLoadContext.Default.Resolving+=(context,name)=>
            {
                if(name.Name=="HBRGOSakiMod")return context.LoadFromAssemblyPath(mod);
                if(name.Name=="sts2")return context.LoadFromAssemblyPath(nativePath);
                foreach(string directory in new[]{dependencies,Path.GetDirectoryName(mod)!,godotDirectory,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "system.io.hashing", "9.0.0", "lib", "net9.0")})
                {string path=Path.Combine(directory,name.Name+".dll");if(File.Exists(path))return context.LoadFromAssemblyPath(path);}
                return null;
            };
            Assembly loaded=AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
            Type healPatch=loaded.GetType("HBRGOSakiMod.Core.Patches.Combat.YanagiPreparedHealCallsitePatch",true)!;
            object coverage=healPatch.GetProperty("Coverage",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            object? Read(object value,string property)=>value.GetType().GetProperty(property)!.GetValue(value);
            var sites=((System.Collections.IEnumerable)Read(coverage,"Sites")!).Cast<object>().Select(site=>new
            {card=((Type)Read(site,"CardType")!).FullName,caller=((MethodInfo)Read(site,"Caller")!).DeclaringType!.FullName,count=Read(site,"Count"),
                sourceField=(Read(site,"Source") as FieldInfo)?.Name,playField=(Read(site,"Play") as FieldInfo)?.Name}).ToArray();
            File.WriteAllText(Path.GetFullPath(args[4])+".heal-catalog.json",JsonSerializer.Serialize(new
            {modSha256=args[1],sites,reviews=Read(coverage,"Reviews"),scanned=Read(coverage,"Scanned"),milliseconds=Read(coverage,"Milliseconds")},new JsonSerializerOptions{WriteIndented=true}));
            typeof(YanagiBootstrap).Assembly.GetType("YanagiStandalone")!.GetMethod("Run")!.Invoke(null,[Path.GetFullPath(args[4])]);
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}









