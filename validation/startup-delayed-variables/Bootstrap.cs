using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class DelayedVariableBootstrap
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length is not (5 or 6) || (args.Length == 6 && args[5] != "--expect-startup-collision"))
                throw new ArgumentException("Expected candidate, SHA256, native profile, GodotSharp, report, optionally --expect-startup-collision; no runtime isolation is supported.");
            Environment.SetEnvironmentVariable("HBR_EXPECT_STARTUP_COLLISION", args.Length == 6 ? "1" : "0");
            string mod = Path.GetFullPath(args[0]), dependencies = Path.GetFullPath(args[2]);
            if (Hash(mod) != args[1].ToUpperInvariant()) throw new Exception("Candidate hash mismatch");
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(dependencies, "profile-manifest.json")));
            foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray())
                if (Hash(Path.Combine(dependencies, file.GetProperty("file").GetString()!)) != file.GetProperty("copySha256").GetString())
                    throw new Exception("Runtime profile hash mismatch: " + file.GetProperty("file").GetString());
            Environment.SetEnvironmentVariable("HBR_NATIVE_PROFILE", manifest.RootElement.GetProperty("name").GetString());
            string godotDirectory = Path.GetDirectoryName(Path.GetFullPath(args[3]))!;
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                if (name.Name == "HBRGOSakiMod") return context.LoadFromAssemblyPath(mod);
                foreach (string directory in new[] { dependencies, Path.GetDirectoryName(mod)!, godotDirectory,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "system.io.hashing", "9.0.0", "lib", "net9.0") })
                {
                    string path = Path.Combine(directory, name.Name + ".dll");
                    if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
                }
                return null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
            typeof(DelayedVariableBootstrap).Assembly.GetType("DelayedVariableChecks")!.GetMethod("Run")!.Invoke(null,
                [mod, dependencies, Path.GetFullPath(args[4])]);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
