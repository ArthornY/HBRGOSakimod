using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class ForcedNumericsBootstrap
{
    public static int Main(string[] args)
    {
        try
        {
            string mod = Path.GetFullPath(args[0]), dependencies = Path.GetFullPath(args[2]);
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mod))) != args[1].ToUpperInvariant()) throw new Exception("Candidate DLL hash mismatch");
            string manifestPath = Path.Combine(dependencies, "profile-manifest.json");
            if (File.Exists(manifestPath))
            {
                using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
                foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray())
                    if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(dependencies, file.GetProperty("file").GetString()!)))) != file.GetProperty("copySha256").GetString())
                        throw new Exception("Profile DLL hash mismatch: " + file.GetProperty("file"));
                Environment.SetEnvironmentVariable("HBR_NATIVE_PROFILE", manifest.RootElement.GetProperty("name").GetString());
            }
            string godotDirectory = Path.GetDirectoryName(Path.GetFullPath(args[3]))!;
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                if (name.Name == "HBRGOSakiMod") return context.LoadFromAssemblyPath(mod);
                foreach (string directory in new[] { dependencies, Path.GetDirectoryName(mod)!, godotDirectory })
                {
                    string path = Path.Combine(directory, name.Name + ".dll");
                    if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
                }
                return null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
            typeof(ForcedNumericsBootstrap).Assembly.GetType("ForcedNumericsChecks")!.GetMethod("Run")!.Invoke(null, [Path.GetFullPath(args[4])]);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
