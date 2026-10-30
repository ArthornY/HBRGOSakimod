using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

static partial class Checks
{
    static Millsage CannonRelic = null!;
    static bool CountCannonPreviews;
    static int CannonDamagePreviews,CannonBlockPreviews;
    static void CountDamagePreview(){if(CountCannonPreviews)CannonDamagePreviews++;}
    static void CountBlockPreview(){if(CountCannonPreviews)CannonBlockPreviews++;}
    static void CannonLocalization()
    {
        var tables=(Dictionary<string,LocTable>)Field(typeof(LocManager),"_tables").GetValue(LocManager.Instance)!;
        foreach(string file in new[]{"monsters","rest_site_ui","intents"})
        {
            var data=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Root,"HBRGOSakiMod/localization/zhs",file+".json")))!;
            tables[file]=new LocTable(file,data,null!);
        }
    }
    static Creature CannonEntity(HotaruCannonModel model)
    {
        Set(model,"<IsMutable>k__BackingField",true);
        var entity=Blank<Creature>();Set(entity,"_powers",new List<PowerModel>());Set(entity,"_maxHp",1);Set(entity,"_currentHp",1);
        Set(entity,"<Side>k__BackingField",CombatSide.Player);Set(entity,"_petOwner",Player);Set(entity,"<Monster>k__BackingField",model);
        Set(model,"_creature",entity);entity.CombatState=Hero.CombatState;
        ((List<Creature>)Field(typeof(CombatState),"_allies").GetValue(Hero.CombatState)!).Add(entity);
        return entity;
    }
    static LocString CannonHover(HotaruCannonModel model)=>(LocString)model.GetType().GetMethod("IntentDescription",F)!.Invoke(model,null)!;
    static string CannonText(HotaruCannonKind kind,int? soul=null)=>(string)Call(Helper("HotaruCannonUpgrades"),"BaseText",null,kind,Player,soul)!;
    static void ValidCannonText(string text,string name)
    {
        text=Clean(text);Check(!text.Contains('{')&&!text.Contains('}'),name+" renders without unresolved placeholders");
        Check(!text.Contains("预计")&&!text.Contains("倍率"),name+" shows numeric effects without estimated/coefficient-first wording");
        Rows.Add(new{name,text});
    }
    static void CannonDisplayCases()
    {
        var relic=new Millsage();Set(relic,"<IsMutable>k__BackingField",true);Set(relic,"_owner",Player);CannonRelic=relic;
        var rainbow=new HotaruIridescence();Set(rainbow,"<IsMutable>k__BackingField",true);Set(rainbow,"_owner",Player);
        ((List<RelicModel>)Field(typeof(Player),"_relics").GetValue(Player)!).AddRange([relic,rainbow]);
        typeof(ElectronicSoldierManual).GetProperty("Talent",F)!.SetValue(Manual,6);
        var models=new HotaruCannonModel[]{new HotaruHappinessCannon(),new HotaruCoffinCannon(),new HotaruContestCannon(),new HotaruLongingCannon(),new HotaruAbsolutionCannon()};
        foreach(var model in models)CannonEntity(model);
        string before=relic.CannonStateData;
        foreach(var model in models)
        {
            LocString text=CannonHover(model);string rendered=text.GetFormattedText();ValidCannonText(rendered,model.Kind+" actual hover");
            foreach(string field in new[]{"Damage","Block","StartDamage","EndDamage"})
                if(text.GetRawText().Contains("{"+field+"}",StringComparison.Ordinal))
                    Check(Clean(rendered).Contains((string)text.Variables[field]),model.Kind+" hover includes exact numeric preview "+field);
            foreach(string prefix in new[]{"HOTARU_","HBRGO_SAKI_MOD_MONSTER_HOTARU_"})
            {
                var alias=new LocString("monsters",prefix+model.Kind.ToString().ToUpperInvariant()+"_CANNON.intentDescription.hbr");
                alias.AddVariablesFrom(text);ValidCannonText(alias.GetFormattedText(),prefix+model.Kind+" localized alias");
            }
        }
        string coffin=Clean(CannonHover(models[1]).GetFormattedText());
        Check(coffin.Contains("获得224点格挡（80%）"),"actual Coffin hover shows screenshot112INT result224 followed by coefficient80percent");
        string longing=Clean(CannonHover(models[3]).GetFormattedText());Check(longing.Contains("获得168点格挡（60%）"),"actual Longing hover uses current INT and final cannon block scale");
        Check(relic.CannonStateData==before,"repeated actual hover reads do not rewrite saved cannon combat state or roll caches");
        foreach(var kind in Enum.GetValues<HotaruCannonKind>().Where(k=>k!=HotaruCannonKind.Endgame))
            ValidCannonText(CannonText(kind),kind+" manual base");
        string rules=(string)Call(Helper("HotaruCannonUpgrades"),"ManualRules",null,Player)!;
        Check(!rules.Contains('{')&&!rules.Contains("预计"),"manual rules render every nested cannon numeric description");
        Check(Clean(rules).Contains("获得224点格挡（80%）"),"manual rules reuse exact live Coffin description");
        Check(Clean(rules).Contains("灵魂之力")&&Clean(rules).Contains("最终伤害倍率"),"Soul Power remains a distinct final damage factor explanation");
        Rows.Add(new{name="manual rules",text=Clean(rules)});

        var final=new HotaruFinalCannon();CannonEntity(final);relic.SetLayers(2);
        typeof(Millsage).GetProperty("RetiredCannonsMask",F)!.SetValue(relic,3);
        LocString positive=CannonHover(final);string positiveText=positive.GetFormattedText();
        ValidCannonText(positiveText,"positive final cannon actual hover");
        Check(Clean(positiveText).Contains("灵魂之力固定80%")&&Clean(positiveText).Contains("基础伤害为2,000点（33.33%"),"positive final cannon distinguishes fixed Soul80percent from base skill33point33percent");
        relic.SetLayers(1);typeof(HotaruIridescence).GetProperty("Layers",F)!.SetValue(rainbow,2);
        typeof(Millsage).GetProperty("RetiredCannonsMask",F)!.SetValue(relic,18);
        string negative=CannonHover(final).GetFormattedText();ValidCannonText(negative,"negative final cannon actual hover");
        Check(Clean(negative).Contains("基础伤害为4,000点（66.67%")&&Clean(negative).Contains("回合开始时，额外造成"),"negative final cannon uses numeric base and separately formatted extra-start attack");
        typeof(Millsage).GetProperty("RetiredCannonsMask",F)!.SetValue(relic,0);typeof(HotaruIridescence).GetProperty("Layers",F)!.SetValue(rainbow,1);

        var combat=(CombatState)Hero.CombatState!;var enemies=Field(typeof(CombatState),"_enemies").GetValue(combat);
        Set(combat,"_enemies",new List<Creature>());
        try{Check(Clean(CannonHover(models[2]).GetFormattedText()).Contains("造成1,000点伤害（33.33%）"),"no-target live cannon fallback converts base units before Soul50percent");}
        finally{Set(combat,"_enemies",enemies);}
        Hero.CombatState=null;
        try
        {
            Check(Clean(CannonText(HotaruCannonKind.Coffin)).Contains("获得224点格挡（80%）"),"out-of-combat menu uses confirmed current INT and final cannon block scale");
            Check(Clean(CannonText(HotaruCannonKind.Contest)).Contains("造成1,000点伤害（33.33%）"),"out-of-combat damage menu uses no-target base and current Soul");
            Check(Clean(CannonText(HotaruCannonKind.Contest,60)).Contains("造成1,200点伤害（33.33%）"),"prospective upgrade menu calculates supplied next Soul without changing authored skill percent");
        }
        finally{Hero.CombatState=combat;}
        Check(relic.CannonStateData==before,"manual and prospective menu previews leave saved combat state unchanged");

        var h=new Harmony("hbr.cannon.display.preview.counts");
        h.Patch(Helper("HotaruCannonStatsHelper").GetMethod("FormatDamagePreview",F)!,prefix:new HarmonyMethod(typeof(Checks).GetMethod(nameof(CountDamagePreview),F)!));
        h.Patch(Helper("HotaruCannonBlockHelper").GetMethod("Preview",F)!,prefix:new HarmonyMethod(typeof(Checks).GetMethod(nameof(CountBlockPreview),F)!));
        CountCannonPreviews=true;CannonDamagePreviews=0;CannonBlockPreviews=0;
        try
        {
            foreach(var kind in Enum.GetValues<HotaruCannonKind>().Where(k=>k!=HotaruCannonKind.Endgame))
            for(int tier=1;tier<=7;tier++)
            {
                string text=(string)Call(Helper("HotaruCannonUpgrades"),"UpgradeText",null,kind,tier,Player)!;
                Check(!text.Contains('{'),kind+" tier"+tier+" formats all upgraded effect variables");
            }
            Check(CannonDamagePreviews==0&&CannonBlockPreviews==0,"all35 upgrade-only templates run zero unused full damage/block previews");
            CannonText(HotaruCannonKind.Coffin);Check(CannonBlockPreviews==1&&CannonDamagePreviews==0,"Coffin template runs only required block preview");
            CannonDamagePreviews=0;CannonBlockPreviews=0;CannonText(HotaruCannonKind.Absolution);
            Check(CannonDamagePreviews==2&&CannonBlockPreviews==0,"Absolution template runs only start and end damage previews");
            CannonDamagePreviews=0;CannonBlockPreviews=0;Call(Helper("HotaruCannonUpgrades"),"ManualRules",null,Player);
            Check(CannonDamagePreviews==4&&CannonBlockPreviews==2,"manual rules evaluate only four damage and two block values required by five cannon descriptions");
        }
        finally{CountCannonPreviews=false;h.UnpatchAll(h.Id);}
        Mode(false);string legacy=CannonText(HotaruCannonKind.Coffin);Check(legacy.Contains("4点基础格挡")&&!legacy.Contains("80%"),"disabled HBR retains legacy authored cannon description");Mode(true);
    }
    public static Task RunCannonDisplay()
    {
        bool ok=false;string? error=null;
        try{Install();Localization();World();CannonLocalization();CannonDisplayCases();ok=true;}
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"report.json"),JsonSerializer.Serialize(new{
            ok,checks=Passed.Count,passed=Passed,rows=Rows,error,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            monsterLocalizationSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,"HBRGOSakiMod/localization/zhs/monsters.json")))),
            restLocalizationSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,"HBRGOSakiMod/localization/zhs/rest_site_ui.json")))),
            limits="Reuses actual card-display native setup and HBR numeric adapters. Runs real cannon IntentDescription, saved Millsage state, actual damage/block preview helpers, real LocManager/LocTable/SmartFormat, and actual manual/rest description builders. Counts real preview helper invocations to reject unused calculations. Hook-listener enumeration, Godot StringName and logging remain isolated leaves. No Godot nodes, rendered UI, game process, packaging or upload."
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" cannon display checks");if(!ok)throw new Exception("Cannon display checks failed; inspect report.json");return Task.CompletedTask;
    }
}
