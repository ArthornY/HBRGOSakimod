using System.Reflection;
using System.Text.Json;
using BaseLib.Config;
using HBRGOSakiMod.Core.Config;

static partial class EffectChecks
{
    static void DisplayConfigCases(string report)
    {
        var property=typeof(HBRGOSakiConfig).GetProperty("ThirtyOneXHiddenEffectPermille")!;
        Check(property.PropertyType==typeof(int)&&property.GetMethod!.IsStatic&&property.CanWrite,"hidden probability is a serializable static integer setting");
        Check(property.GetCustomAttribute<ConfigHideInUI>()!=null&&property.GetCustomAttribute<ConfigIgnoreAttribute>()==null,"probability hides from UI without excluding config persistence");
        var config=Blank<HBRGOSakiConfig>();Set(config,"ConfigProperties",new List<PropertyInfo>());Set(config,"_configFileLock",new SemaphoreSlim(1,1));
        string path=Path.Combine(Path.GetDirectoryName(report)!,"31x-display-config-fixture.json");Set(config,"_path",path);Set(config,"_modConfigName","31XDisplayHost");
        Invoke(typeof(ModConfig).GetMethod("CheckConfigProperties",F)!,config);
        var discovered=(List<PropertyInfo>)typeof(ModConfig).GetField("ConfigProperties",F)!.GetValue(config)!;
        Check(discovered.Contains(property),"actual BaseLib config property discovery includes hidden probability");
        Check(discovered.Where(item=>item.GetCustomAttribute<ConfigHideInUI>()==null).All(item=>item!=property),"actual BaseLib visibility predicate excludes only its menu row");
        // The actual Save/Load implementation sees this isolated property only;
        // no user preferences or user:// paths are read or written.
        discovered.Clear();discovered.Add(property);
        try
        {
            property.SetValue(null,1);config.Save();var saved=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))!;
            Check(saved[property.Name]=="1","actual BaseLib Save writes default1 into the workspace config fixture");
            property.SetValue(null,0);config.Load();Equal((int)property.GetValue(null)!,1m,"actual BaseLib Load restores the persisted default");
            foreach(var pair in new[]{(-25,0),(0,0),(1000,1000),(5000,1000)})
            {
                File.WriteAllText(path,JsonSerializer.Serialize(new Dictionary<string,string>{{property.Name,pair.Item1.ToString(System.Globalization.CultureInfo.InvariantCulture)}}));config.Load();Equal((int)property.GetValue(null)!,pair.Item2,"native file reload clamps probability value"+pair.Item1);
            }
        }
        finally{property.SetValue(null,1);}
    }
}
