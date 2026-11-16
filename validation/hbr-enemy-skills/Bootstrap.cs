using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class Bootstrap
{
    public static int Main(string[] args)
    {
        try
        {
            string mod=Path.GetFullPath(args[0]),dependencies=Path.GetFullPath(args[2]);
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mod)))!=args[1].ToUpperInvariant())throw new Exception("Candidate hash mismatch");
            string report=args.Length>4&&!args[4].StartsWith("--",StringComparison.Ordinal)?Path.GetFullPath(args[4]):Path.Combine(AppContext.BaseDirectory,"report.json");
            Environment.SetEnvironmentVariable("HBR_ENEMY_SKILLS_REPORT",report);
            Environment.SetEnvironmentVariable("HBR_SKIP_HOPPER_CALIBRATION",args.Contains("--skip-hopper-calibration")?"1":"0");
            Environment.SetEnvironmentVariable("HBR_SKIP_ACT_CARRYOVER",args.Contains("--skip-act-carryover")?"1":"0");
            Environment.SetEnvironmentVariable("HBR_HOPPER_ONLY",args.Contains("--hopper-calibration-only")?"1":"0");
            string manifestPath=Path.Combine(dependencies,"profile-manifest.json");
            if(File.Exists(manifestPath))
            {
                using var manifest=JsonDocument.Parse(File.ReadAllText(manifestPath));
                foreach(var file in manifest.RootElement.GetProperty("files").EnumerateArray())
                    if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(dependencies,file.GetProperty("file").GetString()!))))!=file.GetProperty("copySha256").GetString())throw new Exception("Runtime profile hash mismatch");
                Environment.SetEnvironmentVariable("HBR_NATIVE_PROFILE",manifest.RootElement.GetProperty("name").GetString());
            }
            string native=Path.Combine(dependencies,"sts2.dll"),godot=Path.GetDirectoryName(Path.GetFullPath(args[3]))!;
            Environment.SetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(native))));
            if(args.Contains("--isolate-telemetry"))native=TelemetryIsolation.Create(native,report);
            AssemblyLoadContext.Default.Resolving+=(context,name)=>
            {
                if(name.Name=="HBRGOSakiMod")return context.LoadFromAssemblyPath(mod);
                if(name.Name=="sts2")return context.LoadFromAssemblyPath(native);
                foreach(string directory in new[]{dependencies,Path.GetDirectoryName(mod)!,godot})
                {string path=Path.Combine(directory,name.Name+".dll");if(File.Exists(path))return context.LoadFromAssemblyPath(path);}
                return null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
            string entry=args.Contains("--act-carryover")?"RunActCarryover":"Run";
            ((Task)typeof(Bootstrap).Assembly.GetType("Checks")!.GetMethod(entry)!.Invoke(null,null)!).GetAwaiter().GetResult();return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
