using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

static partial class Checks
{
    static readonly List<object> ResourceRows=[];
    static readonly List<object> ResourceCases=[];
    static string ReportPath=>Environment.GetEnvironmentVariable("HBR_RESOURCE_REPORT")??Path.Combine(AppContext.BaseDirectory,"report.json");
    static decimal RatioFloor(decimal value,decimal numerator,decimal denominator)=>decimal.Floor(value*numerator/denominator);
    static object HpDp(decimal hp,decimal dp)=>new{hp,dp};
    public static async Task Run()
    {
        bool ok=false;string? error=null;
        try{await RunCases();ok=true;}
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        string directory=Path.GetDirectoryName(Path.GetFullPath(ReportPath))!;Directory.CreateDirectory(directory);
        string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location)));
        File.WriteAllText(ReportPath,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=hash,runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),nativeSourceSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),boundary="Actual native Creature entry and HP sampler, multiplayer HP scaling, production one-pass health/phase helpers and DP calculators. All 42 original DP/HP fractions are independent fixture input; no tolerance is allowed at floor boundaries. Enablement/ascension, unrelated monster entry animations/powers and heal visuals are controlled leaves; exact saved strings are fixture state. No engine combat, Godot, package or upload."},new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(directory,"resource-profile-values.json"),JsonSerializer.Serialize(new{modSha256=hash,runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),nativeSourceSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),source="monster-req-20261114/data/dp-hp-share.json; exact integer DP/HP fractions",totalPerNativeHp=900,rows=ResourceRows,cases=ResourceCases},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" enemy resource native checks");
        if(!ok)throw new Exception("Inspect "+ReportPath);
    }
    static MonsterModel TableMonster(Type type,int variant)
    {
        var monster=(MonsterModel)RuntimeHelpers.GetUninitializedObject(type);
        Set(monster,"<IsMutable>k__BackingField",true);
        typeof(HbrgoMoveTableMonster).GetProperty("MoveTableVariant",F)!.SetValue(monster,variant);
        return monster;
    }
    static async Task CheckTableProfiles(Type adapter,Type profile)
    {
        using var input=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"dp-hp-share.json")));
        JsonElement[] rows=input.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Check(input.RootElement.GetProperty("totalPerNativeHp").GetInt32()==900&&rows.Length==42&&rows.Select(row=>row.GetProperty("class").GetString()).Distinct().Count()==33,"the independent original-ratio fixture contains 42 variants across33 classes at900 total per native HP");
        var failures=new List<string>();
        void Verify(bool condition,string label)
        {
            if(condition)Check(true,label);
            else {failures.Add(label);ResourceCases.Add(new{kind="failure",label});}
        }
        foreach(JsonElement row in rows)
        {
            string name=row.GetProperty("class").GetString()!,label=row.GetProperty("variant").GetString()!;
            Type type=Mod.GetType("HBRGOSakiMod.Core.Models.Monsters."+name,true)!;
            decimal dp=row.GetProperty("dp").GetDecimal(),hp=row.GetProperty("hp").GetDecimal(),sum=dp+hp;
            var specimen=TableMonster(type,0);
            int count=(int)type.GetProperty("MoveTableVariantCount",F)!.GetValue(specimen)!,variant=-1;
            for(int index=0;index<count;index++)
            {
                object tables=type.GetMethod("GetMoveTables",F)!.Invoke(specimen,[index])!;
                if((string)tables.GetType().GetProperty("EnemyLabel",F)!.GetValue(tables)! == label){variant=index;break;}
            }
            Check(variant>=0,$"the actual {name} table exposes requested variant {label}");
            Enabled=true;Config();NativeAscension=0;
            var selected=TableMonster(type,variant);int normalMin=selected.MinInitialHp,normalMax=selected.MaxInitialHp;
            object selectedProfile=Call(adapter,"GetProfile",Make(monster:selected))!;
            decimal reference=(decimal)profile.GetProperty("NativeReferenceHp",F)!.GetValue(selectedProfile)!,referenceHp=(decimal)profile.GetProperty("ReferenceMaxHp",F)!.GetValue(selectedProfile)!,referenceDp=(decimal)profile.GetProperty("ReferenceMaxDp",F)!.GetValue(selectedProfile)!;
            Verify(referenceHp*dp==referenceDp*hp&&(referenceHp+referenceDp)==reference*900m,$"{label} selects the original integer DP/HP share and900 total coefficient");
                        foreach(int difficulty in new[]{0,8})
            {
                NativeAscension=difficulty;selected=TableMonster(type,variant);
                int minimum=selected.MinInitialHp,maximum=selected.MaxInitialHp;
                foreach(int raw in new[]{minimum,maximum})
                {
                    Config();Creature creature=Make(max:raw,current:raw/2,ascension:difficulty,monster:TableMonster(type,variant));
                    decimal expectedHp=RatioFloor(raw,900m*hp,sum),expectedCurrent=RatioFloor(raw/2,900m*hp,sum);
                    decimal expectedDp=RatioFloor(expectedHp,dp,hp);
                    Initial(creature);
                    Verify(creature.MaxHp==expectedHp&&creature.CurrentHp==expectedCurrent,$"{label} difficulty{difficulty} raw{raw} HP/current floor: actual {creature.MaxHp}/{creature.CurrentHp}, expected {expectedHp}/{expectedCurrent}");
                    Verify(HighDimensionalShellPower.CalculateAmount(creature)==expectedDp,$"{label} difficulty{difficulty} raw{raw} DP floor: actual {HighDimensionalShellPower.CalculateAmount(creature)}, expected {expectedDp}");
                    Initial(creature);Verify(creature.MaxHp==expectedHp,$"{label} repeated summon/room entry does not compound its share");
                    ResourceCases.Add(new{kind="bounds",@class=name,variant=label,difficulty,raw,newHPDP=HpDp(creature.MaxHp,HighDimensionalShellPower.CalculateAmount(creature))});
                }
                bool sampler=true;
                for(uint seed=0;seed<16;seed++)
                {
                    Creature sampled=Make(monster:TableMonster(type,variant));sampled.SetUniqueMonsterHpValue([sampled],new Rng(seed));
                    decimal raw=sampled.MaxHp;Initial(sampled);
                    sampler&=raw>=minimum&&raw<=maximum&&sampled.MaxHp==RatioFloor(raw,900m*hp,sum);
                }
                Verify(sampler,$"{label} difficulty{difficulty} native HP sampler stays within its own table and scales the sampled value once");
            }
            ResourceRows.Add(new{@class=name,variant=label,variantIndex=variant,originalDp=dp,originalHp=hp,rawMin=normalMin,rawMax=normalMax,oldHPDP=new{min=HpDp(normalMin*700m,normalMin*200m),max=HpDp(normalMax*700m,normalMax*200m)},newHPDP=new{min=HpDp(RatioFloor(normalMin,900m*hp,sum),RatioFloor(RatioFloor(normalMin,900m*hp,sum),dp,hp)),max=HpDp(RatioFloor(normalMax,900m*hp,sum),RatioFloor(RatioFloor(normalMax,900m*hp,sum),dp,hp))}});
            // Fresh summons and live phases retain native multiplayer first,
            // then apply independently configured HP and DP act growth once.
            NativeAscension=0;
            foreach(int players in new[]{1,2,4})
            {
                Config(hp:150,hpGrowth:50,dp:40,dpGrowth:50);
                int raw=normalMax;Creature summon=Make(max:raw,current:raw,act:1,monster:TableMonster(type,variant));
                decimal nativeHp=decimal.Floor(Creature.ScaleHpForMultiplayer(raw,null,players,1));
                summon.ScaleMonsterHpForMultiplayer(null,players,1);Initial(summon);
                decimal expectedHp=RatioFloor(nativeHp*2.25m,900m*hp,sum),expectedDp=RatioFloor(expectedHp*3m,dp,hp);
                Verify(summon.MaxHp==expectedHp&&HighDimensionalShellPower.CalculateAmount(summon)==expectedDp,$"{label} summon players{players} preserves native multiplayer, HP setting and separate DP growth: actual {summon.MaxHp}/{HighDimensionalShellPower.CalculateAmount(summon)}, expected {expectedHp}/{expectedDp}");
                decimal actualEntryHp=summon.MaxHp,actualEntryDp=HighDimensionalShellPower.CalculateAmount(summon);
                decimal phase=Replacement(summon,raw);
                Verify(phase==RatioFloor(raw*2.25m,900m*hp,sum)&&Replacement(summon,raw)==phase,$"{label} phase uses raw HP and one profile conversion, independent of earlier summon multiplayer");
                summon.SetMaxHpInternal(phase);
                Verify(HighDimensionalShellPower.CalculateAmount(summon)==RatioFloor(phase*3m,dp,hp),$"{label} phase fresh DP follows updated maximum using the same original share");
                ResourceCases.Add(new{kind="configuration-multiplayer-phase",@class=name,variant=label,players,act=2,configuration=new{hpPercent=150,hpGrowthPercent=50,dpPercent=40,dpGrowthPercent=50},nativeHp,entryHPDP=HpDp(actualEntryHp,actualEntryDp),expectedEntryHPDP=HpDp(expectedHp,expectedDp),phaseHPDP=HpDp(phase,HighDimensionalShellPower.CalculateAmount(summon))});
            }
            Config();
            foreach(string identity in new[]{"ally","pet","phantom","disabled"})
            {
                Enabled=identity!="disabled";
                Creature excluded=Make(max:normalMin,current:normalMin,side:identity=="ally"?CombatSide.Player:CombatSide.Enemy,monster:TableMonster(type,variant));
                if(identity=="pet")Set(excluded,"_petOwner",Blank<Player>());
                if(identity=="phantom")AddPower(excluded,Blank<PhantomPower>());
                Initial(excluded);
                Verify(excluded.MaxHp==normalMin&&HighDimensionalShellPower.CalculateAmount(excluded)==decimal.Floor(normalMin*.2m),$"{label} {identity} bypasses HBR variant-resource adaptation");
                ResourceCases.Add(new{kind="exclusion",@class=name,variant=label,identity,raw=normalMin,newHPDP=HpDp(excluded.MaxHp,HighDimensionalShellPower.CalculateAmount(excluded))});
            }
            Enabled=true;Config();
            Creature hard=Make(max:normalMax,current:normalMax,ascension:10,monster:TableMonster(type,variant));Initial(hard);
            decimal hardDp=RatioFloor(hard.MaxHp*1.5m,dp,hp);
            Verify(HighDimensionalShellPower.CalculateAmount(hard)==hardDp,$"{label} high-difficulty shell enhancement retains its original profile: actual {HighDimensionalShellPower.CalculateAmount(hard)}, expected {hardDp}");
            SuppressEnhancement=true;
            Verify(HighDimensionalShellPower.CalculateAmount(hard)==RatioFloor(hard.MaxHp,dp,hp),$"{label} phantom suppression removes only the enhanced shell multiplier");
            SuppressEnhancement=false;ResourceCases.Add(new{kind="high-difficulty",@class=name,variant=label,ascension=10,hp=hard.MaxHp,dp=HighDimensionalShellPower.CalculateAmount(hard)});
            Config(dp:0);Verify(HighDimensionalShellPower.CalculateAmount(hard)==0,$"{label} the zero-DP setting remains zero with variant resources");Config();
            Creature saved=Make(max:normalMax,current:normalMax,monster:TableMonster(type,variant));Initial(saved);
            var ledger=Blank<HighDimensionalShellPower>();AddPower(saved,ledger);typeof(HighDimensionalShellPower).GetProperty(nameof(HighDimensionalShellPower.CurrentDpData),F)!.SetValue(ledger,"12345678901234567890");
            saved.SetMaxHpInternal(saved.MaxHp+100m);_=HighDimensionalShellPower.CalculateAmount(saved);
            Verify(ledger.CurrentDpData=="12345678901234567890",$"{label} fresh maximum/profile calculation does not rewrite saved current DP");
            ResourceCases.Add(new{kind="saved-current-DP",@class=name,variant=label,before="12345678901234567890",after=ledger.CurrentDpData});
        }
        Check(ResourceRows.Count==42,"all42 variant rows produce independent before/after resource evidence");
        NativeAscension=0;Enabled=true;Config();
        Check(failures.Count==0,string.Join("\n",failures));CheckMaximumResourceFallback();await Task.CompletedTask;
    }
    static void CheckMaximumResourceFallback()
    {
        const decimal cap=18446744073709551616m;
        foreach(MonsterModel monster in new MonsterModel[]{Blank<SmallHopper>(),TableMonster(typeof(DiamondEyeball),0),TableMonster(typeof(SlimySerpent),0)})
        {
            Enabled=true;Config(hp:1000,hpGrowth:1000,dp:1000,dpGrowth:1000);
            Creature huge=Make(max:999999999,current:999999999,act:12,ascension:10,monster:monster);
            var exact=Blank<LargeHealthPower>();AddPower(huge,exact);exact.MaxHpData=cap.ToString(System.Globalization.CultureInfo.InvariantCulture);exact.CurrentHpData=exact.MaxHpData;
            Initial(huge);
            Check(Replacement(huge,cap)==cap,$"{monster.GetType().Name} raw phase at the maximum resource uses overflow fallback then saturates once");
            Check(HighDimensionalShellPower.CalculateAmount(huge)==cap,$"{monster.GetType().Name} maximum saved HP and extreme DP growth use overflow fallback at the final resource cap");
            Check(exact.MaxHpData=="18446744073709551616"&&exact.CurrentHpData=="18446744073709551616",$"{monster.GetType().Name} resource overflow calculation never rewrites exact saved HP strings");
            ResourceCases.Add(new{kind="maximum-resource-overflow",@class=monster.GetType().Name,act=13,ascension=10,hp=exact.MaxHpData,currentHp=exact.CurrentHpData,phaseHp=Replacement(huge,cap),dp=HighDimensionalShellPower.CalculateAmount(huge)});
        }
        Enabled=true;Config();
    }
}
