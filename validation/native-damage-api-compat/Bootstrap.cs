using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

static class Bootstrap
{
    internal static string ModPath = null!, ReportPath = null!;

    public static int Main(string[] args)
    {
        try
        {
            ModPath = Path.GetFullPath(args[0]); ReportPath = Path.GetFullPath(args[4]);
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ModPath))) != args[1].ToUpperInvariant())
                throw new Exception("Candidate SHA256 mismatch");
            string dependencies = Path.GetFullPath(args[2]);
            string manifest = Path.Combine(dependencies, "profile-manifest.json");
            if (File.Exists(manifest))
            {
                using var profile = JsonDocument.Parse(File.ReadAllText(manifest));
                foreach (var file in profile.RootElement.GetProperty("files").EnumerateArray())
                    if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(dependencies,
                        file.GetProperty("file").GetString()!)))) != file.GetProperty("copySha256").GetString())
                        throw new Exception("Profile SHA256 mismatch: " + file.GetProperty("file"));
            }
            string godotDirectory = Path.GetDirectoryName(Path.GetFullPath(args[3]))!;
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                if (name.Name == "HBRGOSakiMod") return context.LoadFromAssemblyPath(ModPath);
                foreach (string directory in new[] { dependencies, Path.GetDirectoryName(ModPath)!, godotDirectory })
                {
                    string path = Path.Combine(directory, name.Name + ".dll");
                    if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
                }
                return null;
            };
            AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dependencies, "sts2.dll"));
            AssemblyLoadContext.Default.LoadFromAssemblyPath(ModPath);
            return (int)typeof(Bootstrap).Assembly.GetType("Checks")!.GetMethod("Run")!.Invoke(null, null)!;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
