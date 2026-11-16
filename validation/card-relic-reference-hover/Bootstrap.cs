using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class CardRelicReferenceBootstrap
{
    static int Main(string[] args)
    {
        try
        {
            if(args.Length<5)throw new ArgumentException("Expected candidate, SHA256, profile, GodotSharp, report [--isolate-telemetry]");
            string candidate=Path.GetFullPath(args[0]),deps=Path.GetFullPath(args[2]),report=Path.GetFullPath(args[4]);
            string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if(Hash(candidate)!=args[1].ToUpperInvariant())throw new Exception("Candidate hash mismatch");
            using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(deps,"profile-manifest.json")));
            foreach(var file in manifest.RootElement.GetProperty("files").EnumerateArray())
                if(Hash(Path.Combine(deps,file.GetProperty("file").GetString()!))!=file.GetProperty("copySha256").GetString())throw new Exception("Profile hash mismatch");
            string native=Path.Combine(deps,"sts2.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            Environment.SetEnvironmentVariable("HBR_NATIVE_PROFILE",manifest.RootElement.GetProperty("name").GetString());
            Environment.SetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256",Hash(native));
            Environment.SetEnvironmentVariable("HBR_NATIVE_RUNTIME_DIRECTORY",deps);
            if(args.Contains("--isolate-telemetry"))native=TelemetryIsolation.Create(native,report);
            AssemblyLoadContext.Default.Resolving+=(context,name)=>{
                if(name.Name=="HBRGOSakiMod")return context.LoadFromAssemblyPath(candidate);
                if(name.Name=="sts2")return context.LoadFromAssemblyPath(native);
                foreach(string dir in new[]{deps,Path.GetDirectoryName(Path.GetFullPath(args[3]))!,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".nuget/packages/system.io.hashing/9.0.0/lib/net9.0")})
                {string file=Path.Combine(dir,name.Name+".dll");if(File.Exists(file))return context.LoadFromAssemblyPath(file);}
                return null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
            typeof(CardRelicReferenceBootstrap).Assembly.GetType("EffectChecks")!.GetMethod("RunReferenceRegression",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,[report]);
            if(Hash(candidate)!=args[1].ToUpperInvariant())throw new Exception("Candidate changed during verification");
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
