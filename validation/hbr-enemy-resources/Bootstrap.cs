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
            string mod = Path.GetFullPath(args[0]);
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mod))) != args[1].ToUpperInvariant())
                throw new Exception("DLL does not match expected SHA256");
            string dependencies = Path.GetFullPath(args[2]);
            Environment.SetEnvironmentVariable("HBR_RESOURCE_REPORT", args.Length > 4 ? Path.GetFullPath(args[4]) : null);
            string manifestPath=Path.Combine(dependencies,"profile-manifest.json");
            if(File.Exists(manifestPath))
            {
                using var manifest=JsonDocument.Parse(File.ReadAllText(manifestPath));
                foreach(var file in manifest.RootElement.GetProperty("files").EnumerateArray())
                    if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(dependencies,file.GetProperty("file").GetString()!))))!=file.GetProperty("copySha256").GetString())throw new Exception("Runtime profile hash mismatch");
                Environment.SetEnvironmentVariable("HBR_NATIVE_PROFILE",manifest.RootElement.GetProperty("name").GetString());
            }
            string godotDirectory = Path.GetDirectoryName(Path.GetFullPath(args[3]))!;
            string nativePath=Path.Combine(dependencies,"sts2.dll");
            Environment.SetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nativePath))));
            if(args.Length>5&&args[5]=="--isolate-telemetry")nativePath=TelemetryIsolation.Create(nativePath,Path.GetFullPath(args[4]));
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                if (name.Name == "HBRGOSakiMod") return context.LoadFromAssemblyPath(mod);
                if (name.Name == "sts2") return context.LoadFromAssemblyPath(nativePath);
                foreach (string directory in new[] { dependencies, Path.GetDirectoryName(mod)!, godotDirectory })
                {
                    string path = Path.Combine(directory, name.Name + ".dll");
                    if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
                }
                return null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(nativePath);
            AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
            ((Task)typeof(Bootstrap).Assembly.GetType("Checks")!.GetMethod("Run")!.Invoke(null, null)!).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
