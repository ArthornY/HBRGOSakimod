using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class DamageCapBootstrap
{
    public static int Main(string[] args)
    {
        try
        {
            string mod=Path.GetFullPath(args[0]), dependencies=Path.GetFullPath(args[2]);
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mod)))!=args[1].ToUpperInvariant())throw new Exception("Candidate hash mismatch");
            string report=Path.GetFullPath(args.Length>4?args[4]:Path.Combine(AppContext.BaseDirectory,"report.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);Environment.SetEnvironmentVariable("HBR_DAMAGE_CAP_REPORT_PATH",report);
            string manifestPath=Path.Combine(dependencies,"profile-manifest.json");
            if(File.Exists(manifestPath))
            {
                using var manifest=JsonDocument.Parse(File.ReadAllText(manifestPath));
                foreach(var file in manifest.RootElement.GetProperty("files").EnumerateArray())
                    if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(dependencies,file.GetProperty("file").GetString()!))))!=file.GetProperty("copySha256").GetString())throw new Exception("Runtime profile hash mismatch");
                Environment.SetEnvironmentVariable("HBR_NATIVE_PROFILE",manifest.RootElement.GetProperty("name").GetString());
            }
            string native=Path.Combine(dependencies,"sts2.dll"),godotDirectory=Path.GetDirectoryName(Path.GetFullPath(args[3]))!;
            Environment.SetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(native))));
            if(args.Length>5&&args[5]=="--isolate-telemetry")native=TelemetryIsolation.Create(native,report);
            AssemblyLoadContext.Default.Resolving+=(context,name)=>
            {
                if(name.Name=="HBRGOSakiMod")return context.LoadFromAssemblyPath(mod);
                if(name.Name=="sts2")return context.LoadFromAssemblyPath(native);
                foreach(string directory in new[]{dependencies,Path.GetDirectoryName(mod)!,godotDirectory})
                {string path=Path.Combine(directory,name.Name+".dll");if(File.Exists(path))return context.LoadFromAssemblyPath(path);}
                return null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(native);AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
            ((Task)typeof(DamageCapBootstrap).Assembly.GetType("Checks")!.GetMethod("Run")!.Invoke(null,null)!).GetAwaiter().GetResult();return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
