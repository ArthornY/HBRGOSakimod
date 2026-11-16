using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class PreviewBootstrap
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length is not (5 or 6)) throw new ArgumentException("Expected candidate, SHA256, native profile, GodotSharp, report; runtime isolation is not supported.");
            Environment.SetEnvironmentVariable("HBR_PREVIEW_INTENT_NEGATIVE", args.Length == 6 && args[5] == "--negative" ? "1" : "0");
            string mod = Path.GetFullPath(args[0]), profile = Path.GetFullPath(args[2]);
            if (Hash(mod) != args[1].ToUpperInvariant()) throw new Exception("Candidate SHA mismatch");
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(profile, "profile-manifest.json")));
            foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray())
                if (Hash(Path.Combine(profile, file.GetProperty("file").GetString()!)) != file.GetProperty("copySha256").GetString()) throw new Exception("Native profile SHA mismatch");
            Environment.SetEnvironmentVariable("HBR_NATIVE_PROFILE", manifest.RootElement.GetProperty("name").GetString());
            Environment.SetEnvironmentVariable("HBR_NATIVE_RUNTIME_DIRECTORY", profile);
            Environment.SetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256", Hash(Path.Combine(profile, "sts2.dll")));
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                if (name.Name == "HBRGOSakiMod") return context.LoadFromAssemblyPath(mod);
                foreach (string directory in new[] { profile, Path.GetDirectoryName(mod)!, Path.GetDirectoryName(Path.GetFullPath(args[3]))!,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "system.io.hashing", "9.0.0", "lib", "net9.0") })
                {
                    string path = Path.Combine(directory, name.Name + ".dll");
                    if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
                }
                return null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
            typeof(PreviewBootstrap).Assembly.GetType("PreviewStandalone")!.GetMethod("Run")!.Invoke(null, [Path.GetFullPath(args[4])]);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}

