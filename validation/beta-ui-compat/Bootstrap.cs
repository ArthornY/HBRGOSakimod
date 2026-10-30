using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class UiBootstrap
{
    static int Main(string[] args)
    {
        try
        {
            string mod = Path.GetFullPath(args[0]), profile = Path.GetFullPath(args[2]);
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mod))) != args[1].ToUpperInvariant())
                throw new Exception("Candidate hash mismatch");
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(profile, "profile-manifest.json")));
            foreach (JsonElement file in manifest.RootElement.GetProperty("files").EnumerateArray())
                if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(profile, file.GetProperty("file").GetString()!)))) !=
                    file.GetProperty("copySha256").GetString()) throw new Exception("Profile dependency hash mismatch");
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                if (name.Name == "HBRGOSakiMod") return context.LoadFromAssemblyPath(mod);
                string dependency = Path.Combine(profile, name.Name + ".dll");
                return File.Exists(dependency) ? context.LoadFromAssemblyPath(dependency) : null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(profile, "sts2.dll"));
            Assembly candidate = AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
            return (int)typeof(UiBootstrap).Assembly.GetType("UiChecks")!.GetMethod("Run")!
                .Invoke(null, [candidate, profile, Path.GetFullPath(args[3])])!;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
