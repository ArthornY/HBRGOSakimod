using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class RandomCharacterUnlockBootstrap
{
    public static int Main(string[] args)
    {
        try
        {
            string mod = Path.GetFullPath(args[0]), profile = Path.GetFullPath(args[2]);
            if (Hash(mod) != args[1].ToUpperInvariant()) throw new Exception("Candidate hash mismatch");
            string manifestPath = Path.Combine(profile, "profile-manifest.json");
            if (File.Exists(manifestPath))
            {
                using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
                foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray())
                    if (Hash(Path.Combine(profile, file.GetProperty("file").GetString()!)) != file.GetProperty("copySha256").GetString())
                        throw new Exception("Runtime profile hash mismatch");
                Environment.SetEnvironmentVariable("HBR_NATIVE_PROFILE", manifest.RootElement.GetProperty("name").GetString());
            }
            string native = Path.Combine(profile, "sts2.dll"), report = Path.GetFullPath(args[4]);
            Environment.SetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256", Hash(native));
            Environment.SetEnvironmentVariable("HBR_RANDOM_EXPECT_OLD", args.Skip(5).Contains("--expect-old-random-lock") ? "1" : "0");
            if (args.Skip(5).Contains("--isolate-telemetry"))
                native = TelemetryIsolation.Create(native, Path.Combine(Path.GetDirectoryName(report)!, "runtime", Path.GetFileNameWithoutExtension(report), "isolation.json"));
            string godotDirectory = Path.GetDirectoryName(Path.GetFullPath(args[3]))!;
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                if (name.Name == "HBRGOSakiMod") return context.LoadFromAssemblyPath(mod);
                if (name.Name == "sts2") return context.LoadFromAssemblyPath(native);
                foreach (string directory in new[] { profile, Path.GetDirectoryName(mod)!, godotDirectory,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "system.io.hashing", "9.0.0", "lib", "net9.0") })
                {
                    string path = Path.Combine(directory, name.Name + ".dll");
                    if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
                }
                return null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
            typeof(RandomCharacterUnlockBootstrap).Assembly.GetType("RandomCharacterUnlockChecks")!.GetMethod("Run")!.Invoke(null, [report]);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
