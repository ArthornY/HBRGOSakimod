using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

static class FragileAttackBootstrap
{
    public static int Main(string[] args)
    {
        try
        {
            string mod = Path.GetFullPath(args[0]);
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mod))) != args[1].ToUpperInvariant())
                throw new Exception("DLL does not match expected SHA256");
            string dependencies = Path.GetFullPath(args[2]);
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
            AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dependencies, "sts2.dll"));
            AssemblyLoadContext.Default.LoadFromAssemblyPath(mod);
            ((Task)typeof(FragileAttackBootstrap).Assembly.GetType("Checks")!
                .GetMethod("RunFragileAttacks")!.Invoke(null, null)!).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
