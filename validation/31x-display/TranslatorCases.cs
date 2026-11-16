using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

static partial class EffectChecks
{
    static Type DisplayTranslatorType=>Helper("ThirtyOneXGlyphTranslator");
    static object DisplayTranslator(Dictionary<string,string[]> words,uint seed)=>(object)Activator.CreateInstance(DisplayTranslatorType,F,null,[words,seed],null)!;
    static string Translate(object translator,ulong surface,string input)=>(string)Invoke(DisplayTranslatorType.GetMethod("Translate",F)!,translator,surface,input)!;
    static Dictionary<string,string[]> DisplayWords()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot,"data/thirty_one_x_character_glossary.json")));
        Check(json.RootElement.GetProperty("schema").GetInt32()==1,"runtime glossary uses schema1");
        var words=json.RootElement.GetProperty("entries").EnumerateObject().ToDictionary(row=>row.Name,row=>row.Value.EnumerateArray().Select(value=>value.GetString()!).ToArray());
        using var inventory=JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot,"data/thirty_one_x_character_glossary.sources.json")));
        var expected=new HashSet<string>();
        foreach(var source in inventory.RootElement.GetProperty("sourceInventory").EnumerateArray())
        {
            using var table=JsonDocument.Parse(File.ReadAllText(source.GetProperty("file").GetString()!));
            foreach(var row in table.RootElement.EnumerateObject().Where(row=>row.Value.ValueKind==JsonValueKind.String))
                foreach(Rune character in row.Value.GetString()!.EnumerateRunes())
                    if((bool)Invoke(DisplayTranslatorType.GetMethod("IsHan",F)!,null,character.Value)!)expected.Add(character.ToString());
        }
        Check(expected.IsSubsetOf(words.Keys),"runtime four-language glossary covers every Han character in its declared localization sources");
        Cases.Add(new{dictionaryEntries=words.Count,expected=expected.Count,missing=expected.Except(words.Keys).ToArray(),extra=words.Keys.Except(expected).ToArray()});return words;
    }
    static void DisplayTranslatorCases(Dictionary<string,string[]> words)
    {
        object all=DisplayTranslator(words,41);int observations=0;
        foreach(var pair in words)
        {
            Check(pair.Value.Length==4&&pair.Value.All(value=>!string.IsNullOrWhiteSpace(value)),"four nonempty lexical glosses: "+pair.Key);
            var allowed=pair.Value.Prepend(pair.Key).ToHashSet();var observed=new HashSet<string>();
            for(ulong surface=0;surface<128;surface++)
            {string result=Translate(all,surface,pair.Key);if(!allowed.Contains(result))throw new Exception("Unexpected dictionary output "+pair.Key);observed.Add(result);observations++;}
            Check(observed.SetEquals(allowed),"all distinct original/four-language outputs are reachable: "+pair.Key);
        }
        Cases.Add(new{dictionaryLanguageObservations=observations});
        Dictionary<string,string[]> sample=new(){["字"]=["<EN>","<IT>","<RU>","<HI>"]};
        object translator=DisplayTranslator(sample,173);
        string input="ABC OD 12.5% [gold]字[/gold] / [img=32]res://图标/能量.png[/img] {伤害:{子项:diff()}} ! \n 字";
        var replacements=new[]{"字","<EN>","<IT>","<RU>","<HI>"};
        for(ulong surface=0;surface<80;surface++)
        {
            string actual=Translate(translator,surface,input);string normalized=Regex.Replace(actual,"字|<EN>|<IT>|<RU>|<HI>","#");
            string expected=input.Replace("字","#");Check(normalized==expected,"protected tags/icons/nested placeholders, Latin/numbers/punctuation/space stay byte-exact at surface"+surface);
        }
        string plain="字 12.5% ABC", refreshed="字 9999.75% XYZ";
        string stable=Translate(translator,519,plain);
        Check(Translate(translator,519,plain)==stable,"identical surface/text result repeats exactly");
        string readback=Translate(translator,600,"字字字 777 [gold]字[/gold]");
        Check(Translate(translator,600,readback)==readback,"translated output assigned to the same surface is idempotent");
        ((IDictionary)DisplayTranslatorType.GetField("_cache",F)!.GetValue(translator)!).Clear();
        Check(Translate(translator,600,readback)==readback,"translated-output readback remains idempotent after input cache clearing");
        Check(Translate(translator,519,refreshed).Replace("9999.75% XYZ","12.5% ABC")==stable,"numeric and Latin refreshes retain the same glyph language");
        Check(Translate(translator,519,"[gold]字[/gold] 12.5% ABC")=="[gold]"+stable.Replace(" 12.5% ABC",string.Empty)+"[/gold] 12.5% ABC","format tag changes do not reroll visible Han choices");
        string repeated=Translate(translator,888,new string('字',256));
        var tokens=Regex.Matches(repeated,"字|<EN>|<IT>|<RU>|<HI>").Select(match=>match.Value).ToArray();
        Check(tokens.Length==256&&tokens.Distinct().Count()==5,"repeated glyph occurrences independently select all five languages by ordinal");
        int[] counts=new int[5];
        for(ulong surface=0;surface<50000;surface++)counts[Array.IndexOf(replacements,Translate(translator,surface,"字"))]++;
        Check(counts.All(count=>count is >8500 and <11500),"five-way language distribution stays near twenty percent");Cases.Add(new{languageDistribution=counts});
        int seedDifferences=0,surfaceDifferences=0;object nextSeed=DisplayTranslator(sample,174);
        for(ulong surface=0;surface<300;surface++)
        {if(Translate(translator,surface,"字")!=Translate(nextSeed,surface,"字"))seedDifferences++;if(Translate(translator,surface,"字")!=Translate(translator,surface+(1UL<<40),"字"))surfaceDifferences++;}
        Check(seedDifferences>180&&surfaceDifferences>180,"independent seed and full64bit surface produce independent choices");
        Check(Translate(translator,1,"unknown ABC 10 "+char.ConvertFromUtf32(0x31350))=="unknown ABC 10 "+char.ConvertFromUtf32(0x31350),"unknown Han extension character remains original");
        foreach(string malformed in new[]{"\ud800字","字\udfff","字[img]bad\ud800[/img]","{broken:字","[broken 字"})
        {string result=Translate(translator,1,malformed);Check(result!=null,"invalid UTF16 or unfinished protected syntax never crashes");if(malformed[0]=='\ud800'||malformed[^1]=='\udfff')Check(result==malformed,"unpaired visible surrogate safely preserves entire original input");}
        string beforeEviction=Translate(translator,47,"字 100");
        for(ulong key=0;key<6000;key++)
        {
            string text="字 "+key;string actual=Translate(translator,key,text),fresh=Translate(DisplayTranslator(sample,173),key,text);
            if(actual!=fresh)throw new Exception("Cache collision at "+key);
            int size=((ICollection)DisplayTranslatorType.GetField("_cache",F)!.GetValue(translator)!).Count;if(size>2048)throw new Exception("Unbounded display cache");
        }
        Check(Translate(translator,47,"字 100")==beforeEviction,"bounded2048cache eviction preserves deterministic recalculation");
        Check(true,"6000fullkey cached results match uncached fresh translators without collisions");
    }
}
