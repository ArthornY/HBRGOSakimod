using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

static class AdditiveDisplayStandalone
{
    public static void Run(string report)=>EffectChecks.RunAdditiveDisplay(report);
}

static partial class EffectChecks
{
    static readonly List<object> AdditiveDisplayRows=[];
    static readonly List<object> AdditiveLocaleFiles=[];
    static string DisplayLanguage="";
    static string PlainDisplay(string text)=>Regex.Replace(text,@"\[[^\]]+\]","");
    static string DisplayText(string name,string text)
    {AdditiveDisplayRows.Add(new{language=DisplayLanguage,source=name,text});return PlainDisplay(text);}
    static void DisplayPercent(string name,string text,string expected)
    {string plain=DisplayText(name,text);Check(plain.Contains(expected+"%",StringComparison.Ordinal),DisplayLanguage+" "+name+" displays "+expected+" percent: "+plain);}
    static IDisposable DisplayHover(AbstractModel model)=>(IDisposable)Call("HbrAdditiveSkillDescription","BeginHover",model)!;
    static void DisplayLocalization(string language)
    {
        DisplayLanguage=language;
        string root=Environment.GetEnvironmentVariable("HBR_ADDITIVE_LOCALE_ROOT")??throw new Exception("Staged locale root is required");
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField",language);
        Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);
        typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        var tables=new Dictionary<string,LocTable>(StringComparer.Ordinal);
        foreach(string table in new[]{"cards","relics","powers","events","potions","static_hover_tips","card_keywords","afflictions","characters","monsters","gameplay_ui","enchantments"})
        {
            var merged=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(string path in new[]{Path.Combine("D:/stsmod/sts/localization",language,table+".json"),
                Path.Combine("D:/stsmod/HBRGOSakiMod/HBRGOSakiMod/localization",language,table+".json"),
                Path.Combine(root,language,table+".json")}.Distinct(StringComparer.Ordinal))
            {
                if(!File.Exists(path))continue;
                foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))!)merged[pair.Key]=pair.Value;
                if(path.StartsWith(root,StringComparison.OrdinalIgnoreCase))
                    AdditiveLocaleFiles.Add(new{language,table,path,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))});
            }
            tables[table]=new LocTable(table,merged,null!);
        }
        Set(manager,"_tables",tables);typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
        foreach(Type type in new[]{typeof(Tainted),typeof(TaintedPower),typeof(VitalSparkPower),typeof(AccuracyPower),typeof(CalcifyPower),
            typeof(PhantomBladesPower),typeof(LeadershipPower),typeof(PhantomPollutionPower),typeof(HbrgoSaki)})
        {
            if(!ModelDb.Contains(type))ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));
        }
        // The accepted mod has no English actor-title tables. Native actor
        // labels keep the real smart-hover chain usable without inventing translations.
        if(language=="eng")
        {
            var character=Blank<MegaCrit.Sts2.Core.Models.Characters.Ironclad>();
            Set(character,"<Id>k__BackingField",ModelDb.GetId(character.GetType()));Set(Player,"<Character>k__BackingField",character);
            var monster=Blank<MegaCrit.Sts2.Core.Models.Monsters.Osty>();
            Set(monster,"<Id>k__BackingField",ModelDb.GetId(monster.GetType()));Set(monster,"_creature",Enemy);Set(Enemy,"<Monster>k__BackingField",monster);
        }
        else
        {
            Set(Player.Character,"<Id>k__BackingField",ModelDb.GetId(Player.Character.GetType()));
            MonsterModel monster=Enemy.Monster??throw new Exception("Display fixture enemy needs its real monster model");
            Set(monster,"<Id>k__BackingField",ModelDb.GetId(monster.GetType()));
        }
    }
    static void InstallAdditiveDisplay()
    {
        var harmony=new Harmony("hbr.additive.display.checks.production");
        foreach(string name in new[]{"HbrAdditiveSkillDescriptionPatch","HbrAdditiveSkillHoverScopePatch","HbrAdditiveSkillTextPatch"})
            Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"installs production "+name);
        harmony.Patch(AccessTools.PropertyGetter(typeof(PowerModel),nameof(PowerModel.Icon)),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
    }
    static string DisplaySmart(PowerModel power)
    {LocString text=power.SmartDescription;Invoke(typeof(PowerModel).GetMethod("AddDumbVariablesToDescription",F)!,power,text,null);text.Add("OnPlayer",power.Owner.IsPlayer);text.Add("IsMultiplayer",false);text.Add("OwnerName","fixture");power.DynamicVars.AddTo(text);return text.GetFormattedText();}
    static Tainted DisplayAffliction(CardModel? card)
    {
        var affliction=(Tainted)ModelDb.Affliction<Tainted>().ToMutable();
        Set(affliction,"_amount",3);if(card!=null){affliction.Card=card;Set(card,"<Affliction>k__BackingField",affliction);}return affliction;
    }
    static void DisplayPowerCases()
    {
        Reset();var tainted=Power<TaintedPower>(Hero,3);var parent=Owned<DefendIronclad>();
        DisplayPercent("Tainted owned description",tainted.GetDumbHoverTip().Description,"50");
        DisplayPercent("Tainted owned smart",DisplaySmart(tainted),"50");
        DisplayPercent("Tainted actual owned hover",string.Join("\n",tainted.HoverTips.OfType<HoverTip>().Select(tip=>tip.Description)),"50");
        LocString remote=tainted.RemoteDescription;remote.Add("Amount",3);DisplayPercent("Tainted owned remote",remote.GetFormattedText(),"50");
        Equal(tainted.Amount,3m,"display never rewrites Tainted power counter");
        var canonical=ModelDb.Power<TaintedPower>();int before=canonical.Amount;
        using(DisplayHover(parent))
        {
            DisplayPercent("canonical FromPower override3",((HoverTip)HoverTipFactory.FromPower<TaintedPower>(3)).Description,"50");
            DisplayPercent("canonical generic rule",((HoverTip)HoverTipFactory.FromPower<TaintedPower>()).Description,"16.67");
            LocString final=canonical.Description;final.Add("Amount",3);DisplayPercent("final variables amount3",final.GetFormattedText(),"50");
            final.Add("Amount",6);DisplayPercent("final variables amount6",final.GetFormattedText(),"100");
            final.Add("Amount",-3);string signed=DisplayText("final variables amount negative",final.GetFormattedText());
            Check(!signed.Contains('%'),"negative final Amount retains native text rather than a positive skill percentage");
        }
        Equal(canonical.Amount,before,"canonical power Amount remains unchanged after native override tips");
        var legacy=Blank<Player>();Set(legacy,"_runState",RunState);Set(legacy,"_relics",new List<RelicModel>());Set(legacy,"<Character>k__BackingField",Player.Character);
        var legacyHero=(Creature)H("Actor",null,legacy,null)!;Set(legacy,"<Creature>k__BackingField",legacyHero);
        var legacyPower=Power<TaintedPower>(legacyHero,3);
        using(DisplayHover(parent))
        {
            string realLegacy=DisplayText("actual nested legacy HoverTips",string.Join("\n",legacyPower.HoverTips.OfType<HoverTip>().Select(tip=>tip.Description)));
            Check(realLegacy.Contains('3')&&!realLegacy.Contains('%'),"real legacy Power.HoverTips shadows HBR parent scope");
            DisplayPercent("restored parent after real legacy hover",((HoverTip)HoverTipFactory.FromPower<TaintedPower>(3)).Description,"50");
            using(DisplayHover(legacyPower))
            {
                string old=DisplayText("nested legacy owned",DisplaySmart(legacyPower));
                Check(old.Contains('3')&&!old.Contains('%'),"nested legacy owner shadows surrounding HBR hover and retains three native points");
                string reference=DisplayText("nested legacy canonical",((HoverTip)HoverTipFactory.FromPower<TaintedPower>(3)).Description);
                Check(!reference.Contains('%'),"nested legacy canonical reference remains native");
            }
            DisplayPercent("restored HBR parent",((HoverTip)HoverTipFactory.FromPower<TaintedPower>(3)).Description,"50");
        }
        Check(!PlainDisplay(((HoverTip)HoverTipFactory.FromPower<TaintedPower>(3)).Description).Contains('%'),"disposing hover restores no-parent native canonical description");
        foreach(var row in new[]{(typeof(AccuracyPower),4,"66.67"),(typeof(CalcifyPower),4,"66.67"),(typeof(PhantomBladesPower),9,"150"),
            (typeof(LeadershipPower),3,"50"),(typeof(PhantomPollutionPower),3,"50")})
        {
            var power=(PowerModel)Invoke(typeof(Checks).GetMethod("Power",F)!.MakeGenericMethod(row.Item1),null,Hero,row.Item2)!;
            DisplayPercent(row.Item1.Name+" owned",LocString.Exists("powers",power.Id.Entry+".title")?power.GetDumbHoverTip().Description:power.Description.GetFormattedText(),row.Item3);
            Equal(power.Amount,row.Item2,"display retains "+row.Item1.Name+" storage");
        }
        var pollution=Power<PhantomPollutionPower>(Hero,3);
        string pollutionText=DisplayText("PhantomPollution expiry",pollution.Description.GetFormattedText());
        Check(pollutionText.Contains(DisplayLanguage=="zhs"?"下次回合开始":"next turn",StringComparison.OrdinalIgnoreCase),"fusion pollution keeps holder-next-start expiry text");
        bool pollutionTitle=LocString.Exists("powers","PHANTOM_POLLUTION_POWER.title");
        string hbrManual=DisplayText("PhantomPollution HBR manual",pollutionTitle?GlossaryEntry("powers","PHANTOM_POLLUTION_POWER",true):LocManager.Instance.GetTable("powers").GetRawText("PHANTOM_POLLUTION_POWER.hbrReferenceDescription"));
        Check(hbrManual.Contains("16.67%")&&!hbrManual.Contains("等同于层数"),"HBR manual describes per-layer additive skill coefficient");
        if(pollutionTitle)
        {
            string oldManual=DisplayText("PhantomPollution legacy manual",GlossaryEntry("powers","PHANTOM_POLLUTION_POWER",false));
            Check(!oldManual.Contains('%'),"legacy fusion-pollution manual retains native point rule");
        }
        else AdditiveDisplayRows.Add(new{language=DisplayLanguage,source="PhantomPollution legacy manual",skipped="Accepted baseline has no localized title/legacy description; new HBR reference template checked directly."});
        Set(tainted,"_amount",0);Check(!PlainDisplay(tainted.GetDumbHoverTip().Description).Contains('%'),"mutable zero power stays native without a minimum positive percentage");
        Set(tainted,"_amount",-3);Check(!PlainDisplay(tainted.GetDumbHoverTip().Description).Contains('%'),"mutable negative power stays native");
    }
    static void DisplayAfflictionCases()
    {
        Reset();var card=Owned<DefendIronclad>();var attached=DisplayAffliction(card);
        DisplayPercent("attached Tainted affliction",attached.DynamicDescription.GetFormattedText(),"50");
        DisplayPercent("attached Tainted extra card",attached.DynamicExtraCardText!.GetFormattedText(),"50");
        string extra=PlainDisplay(attached.DynamicExtraCardText!.GetFormattedText());Check(extra.Contains('3'),"affliction extra text preserves gained three stacks");
        var tips=attached.HoverTips.OfType<HoverTip>().ToArray();Check(tips.Length>=2,"real attached affliction hover has nested native power reference");
        AdditiveDisplayRows.Add(new{language=DisplayLanguage,source="attached affliction full hover",tips=tips.Select(tip=>new{tip.Id,tip.Description}).ToArray()});
        Check(tips.Any(tip=>tip.Id==ModelDb.GetId(typeof(TaintedPower)).ToString()&&PlainDisplay(tip.Description).Contains("50%")),"actual attached-affliction nested Tainted tip resolves final three-stack override");
        using(DisplayHover(card))
        {
            var cardless=HoverTipFactory.FromAffliction<Tainted>(3).OfType<HoverTip>().ToArray();
            Check(cardless.Length>=2&&cardless.Count(tip=>PlainDisplay(tip.Description).Contains("50%"))>=2,"cardless affliction and its amount-overridden nested power inherit HBR parent context");
            var legacy=Blank<Player>();Set(legacy,"_runState",RunState);Set(legacy,"_relics",new List<RelicModel>());
            var legacyHero=(Creature)H("Actor",null,legacy,null)!;Set(legacy,"<Creature>k__BackingField",legacyHero);
            var legacyCard=Owned<DefendIronclad>();Set(legacyCard,"_owner",legacy);
            var legacyAffliction=DisplayAffliction(legacyCard);
            string legacyText=DisplayText("actual attached legacy affliction",string.Join("\n",legacyAffliction.HoverTips.OfType<HoverTip>().Select(tip=>tip.Description)));
            Check(legacyText.Contains('3')&&!legacyText.Contains('%'),"real attached legacy affliction shadows surrounding HBR card hover for nested power references");
            DisplayPercent("HBR scope restored after legacy affliction",((HoverTip)HoverTipFactory.FromPower<TaintedPower>(3)).Description,"50");
        }
        Check(attached.Amount==3&&ReferenceEquals(attached.Card,card),"affliction display preserves amount and exact owner card");
        var spark=Power<VitalSparkPower>(Enemy,3);
        var sparkTips=spark.HoverTips.OfType<HoverTip>().ToArray();
        string sparkText=DisplayText("actual VitalSpark complete hover",string.Join("\n",sparkTips.Select(tip=>tip.Description)));
        Check(sparkText.Contains("50%")&&sparkText.Contains('3'),"actual VitalSpark hover keeps three pollution stacks and fifty-percent skill power");
        Check(sparkTips.Any(tip=>tip.Id==ModelDb.GetId(typeof(TaintedPower)).ToString()&&PlainDisplay(tip.Description).Contains("50%")),"actual VitalSpark nested Tainted reference inherits parent mode and final override");
        Equal(spark.Amount,3m,"VitalSpark display preserves source stacks");
    }
    static void DisplayCardAndRelicCases()
    {
        Reset();
        foreach(var row in new[]{(typeof(Accuracy),"AccuracyPower",4m,6m,"66.67","100"),(typeof(Calcify),"CalcifyPower",4m,6m,"66.67","100"),
#if HBR_BETA_NATIVE
            (typeof(PhantomBlades),"PhantomBladesPower",8m,11m,"133.33","183.33")})
#else
            (typeof(PhantomBlades),"PhantomBladesPower",9m,12m,"150","200")})
#endif
        {
            var card=(CardModel)Activator.CreateInstance(row.Item1)!;Set(card,"<IsMutable>k__BackingField",true);Set(card,"_owner",Player);Set(card,"<Id>k__BackingField",ModelDb.GetId(row.Item1));
            AdditiveDisplayRows.Add(new{language=DisplayLanguage,source=row.Item1.Name+" native card units",units=card.DynamicVars[row.Item2].BaseValue,
                nativeUpgrade=PatchProcessor.GetOriginalInstructions(row.Item1.GetMethod("OnUpgrade",F)!).Select(instruction=>instruction.ToString()).ToArray()});
            DisplayPercent(row.Item1.Name+" card",card.Description.GetFormattedText(),row.Item5);
            Equal(card.DynamicVars[row.Item2].BaseValue,row.Item3,"base card skill unit remains native");
            card.UpgradePreviewType=CardUpgradePreviewType.Combat;card.UpgradeInternal();card.FinalizeUpgradeInternal();
            DisplayPercent(row.Item1.Name+" upgraded card",card.Description.GetFormattedText(),row.Item6);
            Equal(card.DynamicVars[row.Item2].BaseValue,row.Item4,"upgraded card skill unit remains native");
        }
        foreach(var row in new[]{(typeof(FakeStrikeDummy),"ExtraDamage",1m,"16.67"),(typeof(MysticLighter),"Damage",9m,"150")})
        {
            var relic=(RelicModel)Activator.CreateInstance(row.Item1)!;Set(relic,"<IsMutable>k__BackingField",true);Set(relic,"_owner",Player);Set(relic,"<Id>k__BackingField",ModelDb.GetId(row.Item1));
            DisplayPercent(row.Item1.Name+" owned relic",relic.DynamicDescription.GetFormattedText(),row.Item4);
            Equal(relic.DynamicVars[row.Item2].BaseValue,row.Item3,"relic retains raw native additive amount");
            Set(relic,"_owner",null);
            Check(!PlainDisplay(relic.DynamicDescription.GetFormattedText()).Contains('%'),"unowned relic outside merchant scope keeps native text");
            using((IDisposable)Call("HbrNativeEffectSkillHelper","BeginMerchantDisplay",relic,Player)!)
                DisplayPercent(row.Item1.Name+" HBR merchant",relic.DynamicDescription.GetFormattedText(),row.Item4);
            Check(!PlainDisplay(relic.DynamicDescription.GetFormattedText()).Contains('%'),"merchant scope disposes back to native unowned relic");
        }
    }
    static void DisplayFallbackCase()
    {
        Reset();var manager=LocManager.Instance;
        var tables=(Dictionary<string,LocTable>)typeof(LocManager).GetField("_tables",F)!.GetValue(manager)!;
        LocTable original=tables["powers"];
        var local=original.Keys.Where(key=>!key.Contains(".hbrAdditive",StringComparison.Ordinal)).ToDictionary(key=>key,original.GetRawText,StringComparer.Ordinal);
        tables["powers"]=new LocTable("powers",local,original);
        try
        {
            Check(tables["powers"].HasEntry("TAINTED_POWER.hbrAdditiveDescription")&&!tables["powers"].IsLocalKey("TAINTED_POWER.hbrAdditiveDescription"),
                "fallback test has HBR additive template only in another-language table");
            var power=Power<TaintedPower>(Hero,3);
            Check(!PlainDisplay(power.GetDumbHoverTip().Description).Contains('%'),"fallback-only HBR template does not replace current-language native owned description");
            using(DisplayHover(Owned<DefendIronclad>()))
                Check(!PlainDisplay(((HoverTip)HoverTipFactory.FromPower<TaintedPower>(3)).Description).Contains('%'),"fallback-only template does not replace current-language canonical reference");
        }
        finally{tables["powers"]=original;}
    }
    public static void RunAdditiveDisplay(string report)
    {
        bool ok=false;string? error=null;
        try
        {
            Install();InstallAdditiveDisplay();
            foreach(string language in new[]{"zhs","eng"})
            {DisplayLocalization(language);DisplayPowerCases();DisplayAfflictionCases();DisplayCardAndRelicCases();DisplayFallbackCase();}
            ok=true;
        }
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
        File.WriteAllText(report,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=AdditiveDisplayRows,localeFiles=AdditiveLocaleFiles,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Real candidate Harmony getters, LocString final variables and native SmartFormat/FromPower/affliction/VitalSpark hover chains. Stage final locales merged over native and accepted source. Only shared host leaves and Power.Icon resource loading isolated. No game, Godot, packages or saves."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" additive display checks");if(!ok)throw new Exception("Inspect "+report);
    }
}
