using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

static class EquipmentServiceStandalone{public static void Run(string path)=>EffectChecks.RunEquipmentService(path);}
static partial class EffectChecks
{
    static Type Eq(string name)=>Mod.GetType("HBRGOSakiMod.Core.Equipment."+name,true)!;
    static object? EqCall(string type,string name,params object?[]args)=>Invoke(Eq(type).GetMethod(name,F)!,null,args);
    static object EqJson(string type,object value)=>JsonSerializer.Deserialize(JsonSerializer.Serialize(value),Eq(type))!;
    static object? Prop(object value,string name)=>value.GetType().GetProperty(name,F)!.GetValue(value);
    static int Int(object value,string name)=>Convert.ToInt32(Prop(value,name));
    static bool Bool(object value,string name)=>Convert.ToBoolean(Prop(value,name));
    static string Text(object value,string name)=>(string)Prop(value,name)!;
    static object State(ElectronicSoldierManual manual)=>(object)typeof(ElectronicSoldierManual).GetProperty("Equipment",F)!.GetValue(manual)!;
    static string SavedState(ElectronicSoldierManual manual)=>manual.HbrEquipmentData;
    static object Policy(Player player)=>EqCall("HbrSatsukiShopPolicy","Get",player)!;
    static object CommandFor(ElectronicSoldierManual manual,string op,string offer="",string transaction="",int index=-1,int? revision=null,int item=0,int slot=-1)=>EqJson("HbrEquipmentCommand",new{Revision=revision??Int(State(manual),"Revision"),Op=Enum.Parse(Eq("HbrEquipmentOp"),op),Offer=offer,TransactionId=transaction,Index=index,ItemId=item,Slot=slot});
    static Task<bool> Apply(ElectronicSoldierManual manual,object command)=>(Task<bool>)Native(manual,"ApplyEquipmentCommand",command)!;
    static object Transaction(ElectronicSoldierManual manual)=>Prop(State(manual),"LastAlchemy")!;
    static object[] ItemRows(ElectronicSoldierManual manual)=>((System.Collections.IEnumerable)Prop(State(manual),"Items")!).Cast<object>().ToArray();
    static void EquipmentState(ElectronicSoldierManual manual,ulong seed=1UL)=>typeof(ElectronicSoldierManual).GetProperty("HbrEquipmentData",F)!.SetValue(manual,JsonSerializer.Serialize(new{Schema=1,Seed=seed}));
    static ElectronicSoldierManual ServiceReset(params int[] upgrades)
    {
        MachinaReset(0);Set(Player,"<NetId>k__BackingField",101UL);Player.Gold=10000;EquipmentState(Manual);
        var card=BondPermanent<AllOrdersConfirmedHbrgoSaki>();RanaUpgrade(card,upgrades);
        return Manual;
    }
    static object Offer(string id,int tier=0)=>EqCall("HbrEquipmentShop","Find",id,tier)!;
    static object Quote(ElectronicSoldierManual manual,string id)=>Invoke(Eq("HbrEquipmentShop").GetMethods(F).Single(method=>method.Name=="Quote"&&method.GetParameters().Length==2),null,manual,Offer(id,(int)typeof(ElectronicSoldierManual).GetProperty("EquipmentTier",F)!.GetValue(manual)!))!;
    static object Evaluate(ElectronicSoldierManual manual,object command)=>EqCall("HbrEquipmentCommands","Evaluate",State(manual),command,(int)typeof(ElectronicSoldierManual).GetProperty("EquipmentTier",F)!.GetValue(manual)!,true,manual.Owner.Gold,Policy(manual.Owner),manual.Owner.RunState.CurrentActIndex)!;
    static bool ServiceStoreFailure,ServiceBeforeChargeFailure;
    static void StoreFailure(ElectronicSoldierManual __instance)
    {if(ServiceStoreFailure){ServiceStoreFailure=false;throw new InvalidOperationException("controlled-store-after-write");}}
    static void BeforeChargeFailure()
    {if(ServiceBeforeChargeFailure){ServiceBeforeChargeFailure=false;throw new OperationCanceledException("controlled-before-charge");}}
    static bool ServiceNoLocal(ref Player? __result){__result=null;return false;}
    static async Task ExpectTradeFailure(ElectronicSoldierManual manual,object command,string message)
    {
        string data=SavedState(manual),cached=JsonSerializer.Serialize(State(manual),Eq("HbrEquipmentState"));int gold=manual.Owner.Gold;
        try{await Apply(manual,command);throw new Exception("Expected controlled failure");}
        catch(Exception error)when(error.Message==message){Check(true,"55: controlled native transaction failure propagates its original exception: "+message);}
        Check(manual.Owner.Gold==gold&&SavedState(manual)==data&&JsonSerializer.Serialize(State(manual),Eq("HbrEquipmentState"))==cached,"55: failed transaction restores only its owner's actual gold and exact saved equipment snapshot");
        Check(!(bool)FieldInfoFor(typeof(ElectronicSoldierManual),"_isApplyingEquipmentTransaction").GetValue(manual)!,"55: a failed transaction always releases its authoritative owner lease");
    }
    static FieldInfo FieldInfoFor(Type type,string name){for(Type? row=type;row!=null;row=row.BaseType)if(row.GetField(name,F)is{}f)return f;throw new MissingFieldException(name);}
    static ulong FindRefundSeed(ElectronicSoldierManual manual,bool editablePlus=false)
    {
        for(ulong seed=1;seed<1000;seed++){EquipmentState(manual,seed);var outcome=Evaluate(manual,CommandFor(manual,"Buy","craft.ring"));if(Int(outcome,"GoldGain")>0)
            {object row=((System.Collections.IEnumerable)Prop(Prop(outcome,"State")!,"Items")!).Cast<object>().Single();int plus=((Array)Prop(row,"Plus")!).Length;object template=EqCall("HbrEquipmentCatalog","Find",Text(row,"Template"))!;if(!editablePlus||plus>0&&plus<Int(template,"PlusSlots"))return seed;}}
        throw new Exception("No deterministic refund seed");
    }
    static async Task ServicePurchaseCases()
    {
        var manual=ServiceReset(0,3,5);MachinaDeck(Player,2);
        Equal(Int(Quote(manual,"craft.ring"),"Price"),18,"55: authoritative first-craft quote multiplies50 by90percent,80percent,50percent then floors once");
        var command=CommandFor(manual,"Buy","craft.ring");int before=Player.Gold;
        Check(await Apply(manual,command),"55: the real manual command commits a discounted native-gold purchase");
        Equal(Player.Gold,before-18,"55: the actual PlayerCmd.LoseGold uses the server-recomputed18 price");
        Check(ItemRows(manual).Length==1&&Int(Transaction(manual),"PaidPrice")==18,"55: exactly one real item and its gross paid-price transaction commit");
        Equal(Int(Quote(manual,"material.plus_change"),"Price"),18,"55: first-craft consumed the shared per-act half-price receipt, leaving the ordinary ten-percent material discount");
        string saved=SavedState(manual);Check(!await Apply(manual,command)&&SavedState(manual)==saved,"55: a duplicate old purchase revision cannot spend gold, RNG or first-use receipts twice");
        Set(RunState,"_currentActIndex",3);Equal(Int(Quote(manual,"craft.ring"),"Price"),36,"55: fourth-act first purchase receives its own half-price receipt despite sharing the third equipment tier");
        Check(await Apply(manual,CommandFor(manual,"Buy","material.plus_change")),"55: a material purchase can consume that act's first-price discount before alchemy");
        Equal(Int(Quote(manual,"craft.ring"),"Price"),72,"55: after that material purchase fourth-act alchemy no longer receives the half-price factor");
        manual=ServiceReset(5,2);Player.Gold=0;saved=SavedState(manual);var insufficient=CommandFor(manual,"Buy","craft.ring");
        Check(!await Apply(manual,insufficient)&&SavedState(manual)==saved,"55: unaffordable purchase neither rolls RNG nor consumes first-purchase or first-alchemy receipts");
        Player.Gold=10000;Check(await Apply(manual,insufficient),"55: the unchanged failed revision can later succeed once the owner has gold");
        object item=ItemRows(manual).Single();object template=EqCall("HbrEquipmentCatalog","Find",Text(item,"Template"))!;
        Check(Int(template,"Rarity")>=4,"55: the still-available first-alchemy guarantee survives a rejected purchase");
        manual=ServiceReset(4);ulong refundSeed=FindRefundSeed(manual);before=Player.Gold;
        Check(await Apply(manual,CommandFor(manual,"Buy","craft.ring")),"55: an original successful alchemy commits its deterministic refund");
        int refund=Int(Transaction(manual),"Refund");Check(refund==25&&Player.Gold==before-50+25,"55: d100 above50 refunds exactly half of the original gross paid50 once");
        Check(refundSeed>0,"55: refund randomness is drawn from the saved per-owner equipment stream");
    }
    static async Task ServiceFailureCases()
    {
        var manual=ServiceReset(0,2,4,5);FindRefundSeed(manual);var command=CommandFor(manual,"Buy","craft.ring");
        ServiceBeforeChargeFailure=true;await ExpectTradeFailure(manual,command,"controlled-before-charge");
        bool fail=true;void GoldFailure(){if(fail){fail=false;throw new InvalidOperationException("controlled-gold-after-write");}}
        Player.GoldChanged+=GoldFailure;try{await ExpectTradeFailure(manual,command,"controlled-gold-after-write");}finally{Player.GoldChanged-=GoldFailure;}
        int lastGold=Player.Gold;bool failRefund=true;
        void RefundFailure(){int current=Player.Gold;if(failRefund&&current>lastGold){failRefund=false;lastGold=current;throw new InvalidOperationException("controlled-refund-after-write");}lastGold=current;}
        Player.GoldChanged+=RefundFailure;try{await ExpectTradeFailure(manual,command,"controlled-refund-after-write");}finally{Player.GoldChanged-=RefundFailure;}
        ServiceStoreFailure=true;await ExpectTradeFailure(manual,command,"controlled-store-after-write");
        Check(await Apply(manual,command),"55: after all controlled failures the same first-use receipts and RNG can still commit once");
        manual=ServiceReset(5);command=CommandFor(manual,"Buy","material.plus_change");bool entered=false;bool? nested=null;
        void Reenter(){if(entered)return;entered=true;nested=Apply(manual,command).GetAwaiter().GetResult();}
        Player.GoldChanged+=Reenter;try{Check(await Apply(manual,command),"55: an original purchase completes while its native GoldChanged hook reenters the service");}finally{Player.GoldChanged-=Reenter;}
        Check(nested==false&&Int(State(manual),"PlusChange")==1,"55: the same-owner authoritative lease rejects nested double purchase before any second charge or delivery");
        manual=ServiceReset(5);Player peer=MachinaPeer(0);peer.Gold=777;var peerManual=peer.GetRelic<ElectronicSoldierManual>()!;EquipmentState(peerManual);bool peerEntered=false,peerSuccess=false;
        void OtherOwnerTrade(){if(peerEntered)return;peerEntered=true;peerSuccess=Apply(peerManual,CommandFor(peerManual,"Buy","material.plus_change")).GetAwaiter().GetResult();throw new InvalidOperationException("controlled-owner-only-fault");}
        Player.GoldChanged+=OtherOwnerTrade;try{await ExpectTradeFailure(manual,CommandFor(manual,"Buy","material.plus_change"),"controlled-owner-only-fault");}finally{Player.GoldChanged-=OtherOwnerTrade;}
        Check(peerSuccess&&peer.Gold==757&&Int(State(peerManual),"PlusChange")==1,"55: another owner's successful concurrent transaction survives this owner's rollback and is never blocked by its lease");

    }
    static async Task ServiceReforgeCases()
    {
        var manual=ServiceReset(2,4,5,6);FindRefundSeed(manual,true);await Apply(manual,CommandFor(manual,"Buy","craft.ring"));
        object first=Transaction(manual);string id=Text(first,"Id");int paid=Int(first,"PaidPrice"),refund=Int(first,"Refund"),before=Player.Gold;
        var reforge=CommandFor(manual,"Reforge",transaction:id);
        Check(await Apply(manual,reforge),"55: one authoritative paid reforge generates a second candidate");
        Equal(Player.Gold,before-Math.Max(1,paid/2),"55: reforge charges half of gross paid price rather than the refunded net price");
        object pending=Transaction(manual);Check(Bool(pending,"ChoicePending")&&Bool(pending,"ReforgeUsed")&&ItemRows(manual).Length==1,"55: the original remains owned while the paid second candidate stays pending outside inventory");
        Equal(Int(pending,"Refund"),refund,"55: paid reforge neither rerolls nor adds another original-transaction refund");
        before=Player.Gold;string saved=SavedState(manual);
        Check(!await Apply(manual,CommandFor(manual,"Reforge",transaction:id))&&Player.Gold==before&&SavedState(manual)==saved,"55: the same original transaction can never be paid for or rolled a second time");
        var claim=EqJson("HbrEquipmentCommand",new{Op=Enum.Parse(Eq("HbrEquipmentOp"),"Claim"),Claim=new{Receipt="55-pending-material",PlusRelease=1,PlusChange=1,Items=Array.Empty<object>()}});
        Check(await Apply(manual,claim)&&Int(State(manual),"PlusRelease")==1&&Bool(Transaction(manual),"ChoicePending"),"55: independent reward claims remain usable while preserving an already-paid two-choice");
        CombatInProgress(false);int original=Int(Transaction(manual),"OriginalItemId");
        foreach(string op in new[]{"Buy","Sell","Equip","OpenPlus","ChangePlus"})
            Check(!await Apply(manual,CommandFor(manual,op,"craft.charm",id,index:0,item:original,slot:1)),"55: pending two-choice blocks otherwise-valid concurrent "+op+" of the actual original, including out of combat");
        saved=SavedState(manual);typeof(ElectronicSoldierManual).GetProperty("HbrEquipmentData",F)!.SetValue(manual,saved);
        Check(Bool(Transaction(manual),"ChoicePending"),"55: closing/reopening the manual and parsing its actual saved JSON preserves the already-paid two-choice");
        Check(!await Apply(manual,CommandFor(manual,"ChooseReforge",transaction:"wrong-transaction",index:1)),"55: even a current revision cannot select another transaction identity");
        object second=Prop(Transaction(manual),"Second")!;var choose=CommandFor(manual,"ChooseReforge",transaction:id,index:1);
        Check(await Apply(manual,choose),"55: selecting the second candidate commits it as the sole kept item");
        Check(ItemRows(manual).Length==1&&Text(ItemRows(manual).Single(),"Template")==Text(second,"Template")&&Bool(Transaction(manual),"DecisionClosed"),"55: choosing second swaps exactly the original item identity and clears the pending candidate");
        saved=SavedState(manual);Check(!await Apply(manual,choose)&&!await Apply(manual,CommandFor(manual,"ChooseReforge",transaction:id,index:0))&&SavedState(manual)==saved,"55: double-clicking either result is idempotently rejected even with an updated revision");
        manual=ServiceReset(6);await Apply(manual,CommandFor(manual,"Buy","craft.charm"));id=Text(Transaction(manual),"Id");before=Player.Gold;
        Check(await Apply(manual,CommandFor(manual,"ChooseReforge",transaction:id,index:0))&&Player.Gold==before&&ItemRows(manual).Length==1,"55: declining the optional reforge keeps the already-paid original without charging a second fee");
        Check(!await Apply(manual,CommandFor(manual,"Reforge",transaction:id)),"55: a declined original transaction cannot reopen another reforge offer");
    }
    static readonly Queue<int> ForcedShopRolls=new();
    static int? ForcedShopRoll;
    static bool ForceShopRaw(int maxExclusive,ref int __result)
    {if(maxExclusive!=100)return true;if(ForcedShopRolls.Count>0){__result=ForcedShopRolls.Dequeue();return false;}if(ForcedShopRoll is not int roll)return true;ForcedShopRoll=null;__result=roll;return false;}
    static object Generated(string kind,int tier,object policy,bool highest,int raw=30,ulong seed=1)
    {
        ForcedShopRoll=raw;object rng=Activator.CreateInstance(Eq("HbrEquipmentRandom"),F,null,new object[]{seed,0},null)!;
        try{return EqCall("HbrEquipmentGenerator","RollAlchemy",Enum.Parse(Eq("HbrEquipmentKind"),kind),tier,policy,highest,rng)!;}
        finally{ForcedShopRoll=null;}
    }
    static int Stars(object rolled)=>Int(Prop(rolled,"Template")!,"Rarity");
    static async Task ServiceGeneratorAndStateCases()
    {
        var manual=ServiceReset(1,2);object policy=Policy(Player);
        foreach(string kind in new[]{"Ring","Earring","Bracelet","Necklace"})
        {
            Equal(Stars(Generated(kind,0,policy,true,29)),5,"55: "+kind+" first-act highest4 promotes to5 on a real raw29 promotion branch");
            Equal(Stars(Generated(kind,0,policy,true,30)),4,"55: "+kind+" raw30 does not enter the30percent extra-star branch");
            Equal(Stars(Generated(kind,1,policy,true,29)),6,"55: "+kind+" second-act highest5 promotes into an existing6star item");
            Equal(Stars(Generated(kind,2,policy,true,29)),6,"55: "+kind+" third-act first-alchemy highest remains capped at6");
        }
        Equal(Stars(Generated("Charm",0,policy,false,29)),4,"55: Satsuki makes the existing advanced charm actually obtainable at first-act raw29");
        Equal(Stars(Generated("Charm",0,policy,false,30)),3,"55: first-act charm raw30 stays in the ordinary pool at exactly30percent chance");
        Equal(Stars(Generated("Charm",0,policy,true,99)),4,"55: first-alchemy guarantee chooses an existing advanced charm even in actone");
        object necklaceKind=Enum.Parse(Eq("HbrEquipmentKind"),"Necklace");
        object[] necklaceRows=((System.Collections.IEnumerable)EqCall("HbrEquipmentCatalog","OfKind",necklaceKind)!).Cast<object>().ToArray();
        Check(necklaceRows.All(row=>Int(row,"Rarity")!=2),"55: the unchanged original catalog contains no two-star necklace template");
        foreach(bool promotion in new[]{false,true})
        {
            ForcedShopRolls.Enqueue(promotion?0:35);ForcedShopRolls.Enqueue(promotion?0:30);
            object rng=Activator.CreateInstance(Eq("HbrEquipmentRandom"),F,null,new object[]{1UL,0},null)!;
            object fallback;
            try{fallback=EqCall("HbrEquipmentGenerator","RollAlchemy",necklaceKind,0,policy,false,rng)!;}
            finally{ForcedShopRolls.Clear();}
            Equal(Stars(fallback),promotion?3:1,"55: missing original necklace rank2 preserves the controlled nearest-rank rule with promotion="+promotion+", never inventing a template");
        }
        Equal(((System.Collections.IEnumerable)EqCall("HbrEquipmentCatalog","OfKind",necklaceKind)!).Cast<object>().Count(),necklaceRows.Length,"55: exact-rank gap handling never adds or rewrites catalog templates");
        object disabled=Activator.CreateInstance(Eq("HbrSatsukiShopPolicy"))!;
        Equal(Stars(Generated("Charm",0,disabled,false,0)),3,"55: without Satsuki the original first-act advanced-charm exclusion remains unchanged");
        // Same seed and forced first rarity decision isolate only the extra
        // initial slot; all template, table and initial-plus rolls remain real.
        for(ulong seed=1;seed<=4;seed++)
        {
            object noExtra=Activator.CreateInstance(Eq("HbrSatsukiShopPolicy"),F,null,new object[]{true,false,false,true,false,false,false,false,0},null)!;
            object plain=Generated("Earring",0,noExtra,true,30,seed),enhanced=Generated("Earring",0,policy,true,30,seed);
            int rawPlus=((Array)Prop(plain,"Plus")!).Length,extra=((Array)Prop(enhanced,"Plus")!).Length;
            Equal(extra,Math.Min(rawPlus+1,Int(Prop(enhanced,"Template")!,"PlusSlots")),"55: Saturation appends exactly one actually opened plus-slot, capped by the chosen item capacity");
        }
        manual=ServiceReset(2,5);Set(RunState,"_currentActIndex",2);
        await Apply(manual,CommandFor(manual,"Buy","craft.ring"));
        Check(Bool(Transaction(manual),"ForceHighest")&&Int(Transaction(manual),"PaidPrice")==50,"55: third-act first alchemy commits both its own half-price and highest-rank receipt");
        await Apply(manual,CommandFor(manual,"Buy","craft.ring"));
        Check(!Bool(Transaction(manual),"ForceHighest")&&Int(Transaction(manual),"PaidPrice")==100,"55: a second same-act craft consumes neither a second guarantee nor another first-purchase discount");
        Set(RunState,"_currentActIndex",3);await Apply(manual,CommandFor(manual,"Buy","craft.ring"));
        Check(Bool(Transaction(manual),"ForceHighest")&&Int(Transaction(manual),"PaidPrice")==50,"55: fourth-act first alchemy gets fresh receipts even though its equipment tier is identical to actthree");
        manual=ServiceReset(0,5);var card=Player.Deck.Cards.OfType<AllOrdersConfirmedHbrgoSaki>().Single();Player.Deck.RemoveInternal(card);
        Equal(Int(Quote(manual,"material.plus_change"),"Price"),20,"55: removing the permanent Satsuki card immediately disables every store discount");
        manual=ServiceReset(6);await Apply(manual,CommandFor(manual,"Buy","craft.ring"));string previous=Text(Transaction(manual),"Id");
        await Apply(manual,CommandFor(manual,"Buy","craft.charm"));
        Check(ItemRows(manual).Length==2&&Text(Transaction(manual),"Id")!=previous&&!await Apply(manual,CommandFor(manual,"Reforge",transaction:previous)),"55: a later successful alchemy preserves the earlier purchased item and cannot revive its obsolete reforge identity");
        manual=ServiceReset(2,6);
        for(ulong seed=1;seed<100;seed++)
        {
            EquipmentState(manual,seed);object outcome=Evaluate(manual,CommandFor(manual,"Buy","craft.ring"));object prospective=Prop(outcome,"State")!;
            object row=((System.Collections.IEnumerable)Prop(prospective,"Items")!).Cast<object>().Single();object template=EqCall("HbrEquipmentCatalog","Find",Text(row,"Template"))!;
            if(((Array)Prop(row,"Plus")!).Length<Int(template,"PlusSlots"))break;
        }
        await Apply(manual,CommandFor(manual,"Buy","craft.ring"));int original=Int(Transaction(manual),"OriginalItemId");string id=Text(Transaction(manual),"Id");
        Check(!await Apply(manual,CommandFor(manual,"OpenPlus",item:original))&&!Bool(Transaction(manual),"DecisionClosed"),"55: a rejected original-item modification leaves its unused reforge offer available");
        var material=EqJson("HbrEquipmentCommand",new{Op=Enum.Parse(Eq("HbrEquipmentOp"),"Claim"),Claim=new{Receipt="55-original-modification",PlusRelease=1,Items=Array.Empty<object>()}});
        await Apply(manual,material);Check(await Apply(manual,CommandFor(manual,"OpenPlus",item:original)),"55: a real material-backed plus modification succeeds on the original before any paid reforge");
        Check(Bool(Transaction(manual),"DecisionClosed")&&!await Apply(manual,CommandFor(manual,"Reforge",transaction:id)),"55: modifying the original closes its unpaid stale reforge offer in the same committed operation");
    }
    static Task ServiceSerialized<T>(object rawDescriptor,object rawCommand,Player owner)
    {
        var descriptor=(RitsuLibManagedNetActionDescriptor<T>)rawDescriptor;Check(descriptor.ActionType==MegaCrit.Sts2.Core.Entities.Multiplayer.GameActionType.Any,"55: store requests preserve the existing normal Any-phase native managed action");byte[] bytes=descriptor.Serialize((T)rawCommand);T received=descriptor.Deserialize(bytes);
        Check(JsonSerializer.Serialize(received)==JsonSerializer.Serialize((T)rawCommand),"55: equipment command identities and revisions survive the real Ritsu serializer roundtrip");
        return descriptor.Execute(new RitsuLibManagedNetActionContext<T>(received,owner,null!,Blank<GameActionPlayerChoiceContext>()));
    }
    static Task ServiceNet(ElectronicSoldierManual manual,object command)
    {
        var net=Mod.GetType("HBRGOSakiMod.Core.Integration.HbrEquipmentNetActions",true)!;object descriptor=net.GetField("CommandDescriptor",F)!.GetValue(null)!;
        return (Task)Invoke(typeof(EffectChecks).GetMethod(nameof(ServiceSerialized),F)!.MakeGenericMethod(command.GetType()),null,descriptor,command,manual.Owner)!;
    }
    static async Task ServiceNetworkCases()
    {
        var own=ServiceReset(5);Player peer=MachinaPeer(0);peer.Gold=10000;var theirs=peer.GetRelic<ElectronicSoldierManual>()!;EquipmentState(theirs);
        var command=CommandFor(own,"Buy","material.plus_change");await ServiceNet(own,command);
        Check(Player.Gold==9990&&peer.Gold==10000&&Int(State(own),"PlusChange")==1&&Int(State(theirs),"PlusChange")==0,"55: the serialized authoritative action spends only the sending owner's first-purchase discount and inventory");
        await ServiceNet(theirs,CommandFor(theirs,"Buy","material.plus_change"));
        Check(peer.Gold==9980&&Player.Gold==9990,"55: a teammate without Satsuki receives the original20 price and cannot borrow another owner's half-price perk");
        int before=Player.Gold;await ServiceNet(own,command);Equal(Player.Gold,before,"55: a replayed serialized old revision cannot double-buy after network delivery");
    }
    public static void RunEquipmentService(string path)
    {
        bool ok=false;string? error=null;ReportDirectory=Path.GetDirectoryName(Path.GetFullPath(path))!;
        try
        {
            Install();Localization();
            foreach(Type type in MachinaMembers.Concat(new[]{typeof(AllOrdersConfirmedHbrgoSaki),typeof(ThirtyOneCCollection),typeof(MachinaHookProbePower)}))
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            var h=new Harmony(ProductionId+".equipment-service");
            h.Patch(typeof(RelicModel).GetMethod("Flash",F,Type.EmptyTypes)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
            h.Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
            foreach(Type type in new[]{typeof(RunState),typeof(MegaCrit.Sts2.Core.Combat.CombatState)})h.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(RanaListeners),F)!){priority=Priority.First+2});
            h.Patch(Eq("HbrEquipmentRandom").GetMethod("NextInt",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ForceShopRaw),F)!));
            h.Patch(typeof(PlayerCmd).GetMethod("LoseGold",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(BeforeChargeFailure),F)!));
            h.Patch(typeof(ElectronicSoldierManual).GetMethod("StoreEquipment",F)!,postfix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(StoreFailure),F)!));
            foreach(MethodInfo method in typeof(LocalContext).GetMethods(F).Where(method=>method.Name=="GetMe"&&method.ReturnType==typeof(Player)))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ServiceNoLocal),F)!));
            foreach(MethodInfo method in typeof(SfxCmd).GetMethods(F).Where(method=>method.Name=="Play"))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
            ServicePurchaseCases().GetAwaiter().GetResult();ServiceFailureCases().GetAwaiter().GetResult();ServiceReforgeCases().GetAwaiter().GetResult();ServiceNetworkCases().GetAwaiter().GetResult();ServiceGeneratorAndStateCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),nativeSourceSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),boundary="Actual production manual transaction/equipment JSON and Ritsu descriptor dispatch, native PlayerCmd gold mutations and GoldChanged failure/reentry hooks. Sfx/scene refresh are controlled; before-charge/store-after-write faults are deliberate validation leaves. No Godot, live peer transport, packaging or upload."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" equipment service checks");if(!ok)throw new Exception("Inspect "+path);
    }
}
