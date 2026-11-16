using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class NararaHpDisplayBootstrap
{
    public static int Main(string[] args)
    {
        try
        {
            if(args.Length<6)throw new ArgumentException("Expected candidate, SHA256, native profile, GodotSharp, report and staged localization root, optionally --isolate-telemetry.");
            string mod=Path.GetFullPath(args[0]),profile=Path.GetFullPath(args[2]),report=Path.GetFullPath(args[4]);
            if(Hash(mod)!=args[1].ToUpperInvariant())throw new Exception("Candidate SHA mismatch");
            using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(profile,"profile-manifest.json")));
            foreach(var file in manifest.RootElement.GetProperty("files").EnumerateArray())
                if(Hash(Path.Combine(profile,file.GetProperty("file").GetString()!))!=file.GetProperty("copySha256").GetString())throw new Exception("Native profile SHA mismatch");
            Environment.SetEnvironmentVariable("HBR_NATIVE_PROFILE",manifest.RootElement.GetProperty("name").GetString());
            string native=Path.Combine(profile,"sts2.dll");
            Environment.SetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256",Hash(native));
            if(args.Contains("--isolate-telemetry"))native=TelemetryIsolation.Create(native,report);
            Environment.SetEnvironmentVariable("HBR_NARARA_HP_LOCALE_ROOT",Path.GetFullPath(args[5]));
            AssemblyLoadContext.Default.Resolving+=(context,name)=>
            {
                if(name.Name=="HBRGOSakiMod")return context.LoadFromAssemblyPath(mod);
                if(name.Name=="sts2")return context.LoadFromAssemblyPath(native);
                foreach(string directory in new[]{profile,Path.GetDirectoryName(mod)!,Path.GetDirectoryName(Path.GetFullPath(args[3]))!,
                    "C:/Users/ROG/.nuget/packages/system.io.hashing/9.0.0/lib/net9.0"})
                {string path=Path.Combine(directory,name.Name+".dll");if(File.Exists(path))return context.LoadFromAssemblyPath(path);}
                return null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
            typeof(NararaHpDisplayBootstrap).Assembly.GetType("NararaHpDisplayStandalone")!.GetMethod("Run")!.Invoke(null,[report]);
            if(Hash(mod)!=args[1].ToUpperInvariant())throw new Exception("Candidate changed during checks");
            return 0;
        }
        catch(Exception failure){Console.Error.WriteLine(failure);return 1;}
    }
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
