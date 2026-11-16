using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Relics.Visibility;

static partial class EffectChecks
{
    static readonly Type[] ReferenceCards=[typeof(AcePlayerIsNotMeHbrgoSaki),typeof(AsTheYoungLadyCommandsHbrgoSaki),
        typeof(DiveAsDeepAsIWantHbrgoSaki),typeof(EldestOshimaIsHereHbrgoSaki),typeof(IWillNotFallHbrgoSaki),
        typeof(ItsNinjaShowtimeHbrgoSaki),typeof(YourCombatDeliveryIsHereHbrgoSaki),typeof(MakeAChangeKillYourselfHbrgoSaki),
        typeof(TengenBrilliantMoveHbrgoSaki),typeof(PaintTheWorldMyColorHbrgoSaki)];
    static bool FullTextHeld;
    static int HiddenPluralReads;
    static bool HeldLeaf(ref bool __result){__result=FullTextHeld;return false;}
    static bool ControllerLeaf(ref bool __result){__result=false;return false;}
    static void ObservePlural(RelicModel __instance){if(__instance is Overclock)HiddenPluralReads++;}
    public static void RunReferenceRegression(string report)
    {
        var rows=new List<object>();int checks=0;string? fatal=null;
        try
        {
            InitializeReferenceHost();
            var h=new Harmony("hbr.inspect.reference.regression");
            h.Patch(AccessTools.Method(Helper("HbrHoverDetail"),"IsHeld"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(HeldLeaf),F)!));
            h.Patch(AccessTools.Method(Helper("HbrHoverDetail"),"IsUsingController"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ControllerLeaf),F)!));
            h.Patch(AccessTools.PropertyGetter(typeof(RelicModel),"HoverTips"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ObservePlural),F)!));
            foreach(string name in new[]{"HbrCompactTextFormatPatch","HbrCompactKeywordCachePatch"})PatchType(h,Mod,"HBRGOSakiMod.Core.Patches."+name);
            void Assert(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
            foreach(bool source in new[]{false,true})
            {
                RelicSourceEnabled=source;
                var overclock=ModelDb.Relic<Overclock>();
                Assert(!((IModRelicVisibility)overclock).IsRelicVisible,"Overclock remains hidden from inventory");
                Assembly ritsu=Assembly.Load("STS2-RitsuLib.Runtime");
                var visibility=ritsu.GetType("STS2RitsuLib.Relics.Visibility.Patches.RelicModelHoverTipsVisibilityPatch",true)!.GetMethod("Prefix",F)!;
                object?[] state=[overclock,null];bool keepNative=(bool)visibility.Invoke(null,state)!;
                Assert(!keepNative&&state[1] is IEnumerable<IHoverTip> hidden&&!hidden.Any(),"Actual Ritsu hidden-inventory prefix still supplies an empty collection");
                if(!source)Assert(!overclock.HoverTips.Any(),"Hidden stock plural getter remains empty when external source postfix is disabled");
                var visible=ModelDb.Relic<BurningBlood>();
                Assert(visible.HoverTips.Any(),"Ordinary visible native relic retains native tips");
                HoverTip reference=overclock.HoverTip;
                Assert(reference.CanonicalModel==overclock&&!string.IsNullOrEmpty(reference.Description),"Single native concept tip retains the canonical Overclock and description");
                foreach(Type type in ReferenceCards)
                foreach(string kind in new[]{"canonical","ownerless","owned"})
                foreach(bool upgrade in new[]{false,true})
                {
                    if(kind=="canonical"&&upgrade)continue;
                    var canonical=ModelDb.GetById<CardModel>(ModelDb.GetId(type));
                    CardModel card=kind=="canonical"?canonical:(CardModel)canonical.MutableClone();
                    if(kind=="owned")Set(card,"_owner",Player);
                    if(upgrade){card.UpgradePreviewType=CardUpgradePreviewType.Deck;card.UpgradeInternal();card.FinalizeUpgradeInternal();}
                    int before=HiddenPluralReads;
                    var nativeTips=card.HoverTips.OfType<HoverTip>().ToArray();
                    Assert(HiddenPluralReads==before,"Card references must not enumerate hidden inventory tooltips: "+type.Name);
                    var nativeReferences=nativeTips.Where(t=>t.CanonicalModel is Overclock).ToArray();
                    Assert(nativeReferences.Length==1,"Every card gets exactly one native Overclock reference: "+type.Name);
                    Assert(nativeReferences[0].Title==reference.Title&&nativeReferences[0].Description==reference.Description,"Native concept reference preserves full original text: "+type.Name);
                    foreach(bool full in new[]{false,true})
                    {
                        FullTextHeld=full;
                        int start=HiddenPluralReads;
                        var tips=((IEnumerable<IHoverTip>)Call("HbrCompactHoverTipHelper","ForCard",card)!).OfType<HoverTip>().ToArray();
                        Assert(HiddenPluralReads==start,"Compact/full display never uses hidden inventory getter: "+type.Name);
                        var refs=tips.Where(t=>t.CanonicalModel is Overclock).ToArray();
                        Assert(refs.Length==1&&!string.IsNullOrEmpty(refs[0].Description),"Compact/full display preserves Overclock concept and text: "+type.Name);
                        if(full)Assert(refs[0].Description==reference.Description,"Shift full display uses complete original Overclock description: "+type.Name);
                        rows.Add(new{card=type.Name,kind,upgrade,source,full,tips=tips.Length,overclock=refs[0].Description});
                    }
                }
            }
        }
        catch(Exception e){fatal=e.ToString();Console.Error.WriteLine(e);}
        File.WriteAllText(report,JsonSerializer.Serialize(new{ok=fatal==null,checks,rows,fatal,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSourceSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),
            boundary="Real native card/relic hover getters, BaseLib source postfix and Ritsu hidden prefix. Texture/logging/platform-dependent settings and key-state leaves are controlled. Passive counter asserts no hidden inventory-list reads. No Godot renderer, installed file/config/save changes, or live UI input."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((fatal==null?"PASS ":"FAIL ")+checks+" hidden-relic reference checks");
        if(fatal!=null)throw new Exception("Regression failed; see "+report);
    }
}
