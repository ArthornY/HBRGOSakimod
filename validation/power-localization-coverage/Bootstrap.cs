using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class PowerLocalizationBootstrap
{
    static int Main(string[] args)
    {
        try
        {
            if(args.Length<7)throw new ArgumentException("Expected candidate, SHA256, native profile, repository, alias plan, report, language [--overlay] [--expect-missing] [--isolate-telemetry] [--minimal]");
            string candidate=Path.GetFullPath(args[0]),profile=Path.GetFullPath(args[2]),report=Path.GetFullPath(args[5]);
            string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if(Hash(candidate)!=args[1].ToUpperInvariant())throw new Exception("Candidate hash mismatch");
            using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(profile,"profile-manifest.json")));
            foreach(var file in manifest.RootElement.GetProperty("files").EnumerateArray())
                if(Hash(Path.Combine(profile,file.GetProperty("file").GetString()!))!=file.GetProperty("copySha256").GetString())throw new Exception("Native profile changed");
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            string native=Path.Combine(profile,"sts2.dll");
            Environment.SetEnvironmentVariable("HBR_NATIVE_PROFILE",manifest.RootElement.GetProperty("name").GetString());
            Environment.SetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256",Hash(native));
            if(args.Contains("--isolate-telemetry"))native=TelemetryIsolation.Create(native,report);
            AssemblyLoadContext.Default.Resolving+=(context,name)=>{
                if(name.Name=="HBRGOSakiMod")return context.LoadFromAssemblyPath(candidate);
                if(name.Name=="sts2")return context.LoadFromAssemblyPath(native);
                foreach(string dir in new[]{profile,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".nuget/packages/system.io.hashing/9.0.0/lib/net9.0")})
                {string path=Path.Combine(dir,name.Name+".dll");if(File.Exists(path))return context.LoadFromAssemblyPath(path);}
                return null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
            int result=(int)typeof(PowerLocalizationBootstrap).Assembly.GetType("PowerLocalizationCoverage")!.GetMethod("Run",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,[args])!;
            if(Hash(candidate)!=args[1].ToUpperInvariant())throw new Exception("Candidate changed during validation");
            return result;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
