using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Integration;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;

static class OverchargeChecks { public static void Run(string path)=>DreadChecks.Run(path); }
static class DreadChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Native=typeof(ModelDb).Assembly,Mod=typeof(HbrgoSaki31O).Assembly;
    static readonly Harmony H=new("hbr.dread-selection.native-checks");
    static readonly List<string> Passed=[];
    static readonly Dictionary<object,bool> Enabled=new(ReferenceEqualityComparer.Instance);
    static readonly List<object> Added=[];
    static CharacterModel[] Catalog=[];
    static UnlockState Unlocks=null!;
    static object Save=null!;
    static ProgressState Progress=null!;
    static bool ThirtyAUnlocked,FailVisual;
    static HbrgoSakiVariant Preferred=HbrgoSakiVariant.ThirtyOneO;
    static Type? Dread=>Native.GetType("MegaCrit.Sts2.Core.Nodes.Screens.Dread.NDreadScreen");
    static string Root {get{for(DirectoryInfo? d=new(AppContext.BaseDirectory);d!=null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"HBRGOSakiMod.csproj")))return d.FullName;throw new DirectoryNotFoundException();}}
    static object Blank(Type type){object v=RuntimeHelpers.GetUninitializedObject(type);GC.SuppressFinalize(v);return v;}
    static T Blank<T>()=>(T)Blank(typeof(T));
    static FieldInfo Field(Type t,string name){for(Type? x=t;x!=null;x=x.BaseType)if(x.GetField(name,F|BindingFlags.DeclaredOnly)is{} f)return f;throw new MissingFieldException(t.FullName,name);}
    static void Set(object v,string name,object? data)=>Field(v.GetType(),name).SetValue(v,data);
    static object? Get(object v,string name)=>Field(v.GetType(),name).GetValue(v);
    static object? Invoke(MethodInfo method,object? owner,params object?[] args){try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? Call(object obj,string name,params object?[] args)=>Invoke(obj.GetType().GetMethod(name,F)!,obj,args);
    static object? Static(Type t,string name,params object?[] args)=>Invoke(t.GetMethod(name,F)!,null,args);
    static void Check(bool value,string name){if(!value)throw new Exception(name);Passed.Add(name);}
    static void Patch(MethodBase target,string name)
    {
        if(target is MethodInfo m)target=m.DeclaringType!.GetMethod(m.Name,F|BindingFlags.DeclaredOnly,null,m.GetParameters().Select(p=>p.ParameterType).ToArray(),null)!;
        H.Patch(target,prefix:new HarmonyMethod(typeof(DreadChecks).GetMethod(name,F)!));
    }
    static bool Skip()=>false;
    static bool NameCtor(StringName __instance){GC.SuppressFinalize(__instance);return false;}
    static bool CatalogLeaf(ref IEnumerable<CharacterModel> __result){__result=Catalog;return false;}
    static bool EncountersLeaf(ref IEnumerable<EncounterModel> __result){__result=[];return false;}
    static bool SaveLeaf(ref SaveManager __result){__result=(SaveManager)Save;return false;}
    static bool ProgressLeaf(ref ProgressState __result){__result=Progress;return false;}
    static bool UnlockLeaf(ref UnlockState __result){__result=Unlocks;return false;}
    static bool TextureLeaf(ref CompressedTexture2D __result){__result=null!;return false;}
    static bool VisibleLeaf(ref bool __result){__result=true;return false;}
    static bool PathLeaf(ref NodePath __result){__result=null!;return false;}
    static bool EnableLeaf(object __instance){Enabled[__instance]=true;return false;}
    static bool DisableLeaf(object __instance){Enabled[__instance]=false;return false;}
    static bool ZeroLeaf(ref int __result){__result=0;return false;}
    static bool PreferredLeaf(ref HbrgoSakiVariant __result){__result=Preferred;return false;}
    static bool UnlockedLeaf(ref bool __result){__result=ThirtyAUnlocked;return false;}
    static bool VisualLeaf(){if(FailVisual)throw new InvalidOperationException("controlled Dread background failure");return false;}
    static NCharacterSelectButton InstantiateButton(string unused)
    {
        var button=Blank<NCharacterSelectButton>();Set(button,"_icon",Blank<TextureRect>());Set(button,"_lock",Blank<TextureRect>());
        Set(button,"_remoteSelectedPlayers",new HashSet<ulong>());return button;
    }
    static void AddChildLeaf(Node parent,Node child)=>Added.Add(child);
    static IEnumerable<CodeInstruction> SceneLeaves(IEnumerable<CodeInstruction> source)
    {
        foreach(var c in source)
        {
            if(c.operand is MethodInfo m&&m.DeclaringType==typeof(SceneHelper)&&m.Name=="Instantiate"&&m.IsGenericMethod&&m.GetGenericArguments()[0]==typeof(NCharacterSelectButton))
            {c.opcode=OpCodes.Call;c.operand=typeof(DreadChecks).GetMethod(nameof(InstantiateButton),F)!;}
            else if(c.operand is MethodInfo m2&&m2.DeclaringType?.Name=="GodotTreeExtensions"&&m2.Name=="AddChildSafely")
            {c.opcode=OpCodes.Call;c.operand=typeof(DreadChecks).GetMethod(nameof(AddChildLeaf),F)!;}
            yield return c;
        }
    }
    static void Install()
    {
        TestMode.IsOn=true;
        Patch(typeof(StringName).GetConstructor([typeof(string)])!,nameof(NameCtor));
        Type logger=Native.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
        foreach(var m in logger.GetMethods(F).Where(m=>m.Name is "GetIsRunningFromGodotEditor" or "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose"))Patch(m,nameof(Skip));
        Patch(typeof(ModelDb).GetProperty("AllCharacters",F)!.GetMethod!,nameof(CatalogLeaf));
        Patch(typeof(ModelDb).GetProperty("AllEncounters",F)!.GetMethod!,nameof(EncountersLeaf));
        Patch(typeof(SaveManager).GetProperty("Instance",F)!.GetMethod!,nameof(SaveLeaf));
        Patch(typeof(SaveManager).GetProperty("Progress",F)!.GetMethod!,nameof(ProgressLeaf));
        Patch(typeof(SaveManager).GetMethod("GenerateUnlockStateFromProgress",F)!,nameof(UnlockLeaf));
        foreach(string p in new[]{"CharacterSelectIcon","CharacterSelectLockedIcon"})Patch(typeof(CharacterModel).GetProperty(p,F)!.GetMethod!,nameof(TextureLeaf));
        Patch(typeof(TextureRect).GetProperty("Texture")!.SetMethod!,nameof(Skip));
        Patch(typeof(CanvasItem).GetProperty("Visible")!.GetMethod!,nameof(VisibleLeaf));Patch(typeof(CanvasItem).GetProperty("Visible")!.SetMethod!,nameof(Skip));
        Patch(typeof(Node).GetMethod("GetPath",Type.EmptyTypes)!,nameof(PathLeaf));
        foreach(string p in new[]{"FocusNeighborLeft","FocusNeighborRight","FocusNeighborTop","FocusNeighborBottom"})Patch(typeof(Control).GetProperty(p)!.SetMethod!,nameof(Skip));
        Type button=Native.GetType("MegaCrit.Sts2.Core.Nodes.GodotExtensions.NButton",true)!;
        Patch(button.GetMethod("Enable",F)!,nameof(EnableLeaf));Patch(button.GetMethod("Disable",F)!,nameof(DisableLeaf));
        Patch(typeof(NCharacterSelectButton).GetMethod("RefreshState",F)!,nameof(Skip));
        Save=Blank<SaveManager>();
        Progress=Blank<ProgressState>();Set(Progress,"_characterStats",new Dictionary<ModelId,CharacterStats>());
        Unlocks=Blank<UnlockState>();Set(Unlocks,"_unlockedEpochIds",new HashSet<string>(MegaCrit.Sts2.Core.Timeline.EpochModel.AllEpochIds));Set(Unlocks,"_encountersSeen",new HashSet<ModelId>());
        foreach(Type type in new[]{typeof(MegaCrit.Sts2.Core.Models.Characters.Ironclad),typeof(HbrgoSaki),typeof(HbrgoSaki31O),typeof(HbrgoSaki30A),typeof(HbrgoHotaru)})ModelDb.Inject(type);
        CharacterModel saki=ModelDb.Character<HbrgoSaki>(),o=ModelDb.Character<HbrgoSaki31O>(),a=ModelDb.Character<HbrgoSaki30A>(),hotaru=ModelDb.Character<HbrgoHotaru>();
        Catalog=[ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(),saki,o,a,hotaru,hotaru];
        var runtime=Assembly.Load("STS2-RitsuLib.Runtime");
        Type scopes=runtime.GetType("STS2RitsuLib.Scaffolding.Characters.Patches.CharacterVanillaSelectionPolicyPatches",true)!;
        H.Patch(typeof(NCharacterSelectButton).GetMethod("Init",F)!,prefix:new HarmonyMethod(scopes.GetMethod("Prefix",F)!),finalizer:new HarmonyMethod(scopes.GetMethod("Finalizer",F)!));
        Type all=runtime.GetType("STS2RitsuLib.Scaffolding.Characters.Patches.CharacterVanillaSelectionPolicyAllCharactersPatch",true)!;
        H.Patch(typeof(ModelDb).GetProperty("AllCharacters",F)!.GetMethod!,postfix:new HarmonyMethod(all.GetMethod("Postfix",F)!));
        H.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.CharacterCatalogHotaruNormalizationPatch",true)!).Patch();
        Localization();
        if(Dread is{} dread)
        {
            H.Patch(dread.GetMethod("InitCharacterButtons",F)!,transpiler:new HarmonyMethod(typeof(DreadChecks).GetMethod(nameof(SceneLeaves),F)!));
            Patch(dread.GetMethod("SetSelectedCharacterVisuals",F)!,nameof(VisualLeaf));
            foreach(string m in new[]{"InitChainForCharacter","RefreshDreadRecordsUi","PlayerChanged"})Patch(dread.GetMethod(m,F)!,nameof(Skip));
            Patch(dread.GetMethod("OnSubmenuOpened",F)!,nameof(Skip));
            Type ascension=Native.GetType("MegaCrit.Sts2.Core.Helpers.AscensionHelper",true)!;Patch(ascension.GetMethod("GetCurrentRotatingDreadWinStreak",F)!,nameof(ZeroLeaf));
            Type lobby=Native.GetType("MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby",true)!;Patch(lobby.GetMethod("SetSingleplayerAscensionAfterCharacterChanged",F)!,nameof(Skip));
            Type preference=Mod.GetType("HBRGOSakiMod.Core.Integration.HbrgoSakiVariantPreference",true)!;
            Patch(preference.GetMethod("GetPreferredVariant",F)!,nameof(PreferredLeaf));Patch(preference.GetMethod("IsThirtyAUnlocked",F)!,nameof(UnlockedLeaf));Patch(preference.GetMethod("SetPreferredVariant",F)!,nameof(Skip));
        }
    }
    static void Localization()
    {
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField","zhs");Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);
        typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        var text=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Root,"HBRGOSakiMod/localization/zhs/characters.json")))!;
        Set(manager,"_tables",new Dictionary<string,LocTable>{{"characters",new LocTable("characters",text,null!)}});typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
        foreach(string id in new[]{"HBRGO_SAKI","HBRGO_SAKI_31O","HBRGO_SAKI_30A","HBRGO_SAKI_MOD_CHARACTER_HBRGO_HOTARU","HBRGO_HOTARU"})
        {
            string actual=new LocString("characters",id+".shortTitle").GetFormattedText();Check(!string.IsNullOrWhiteSpace(actual)&&!actual.Contains(id,StringComparison.OrdinalIgnoreCase),"real zhs shortTitle resolves "+id);
        }
    }
    static object Screen()
    {
        object screen=Blank(Dread!),lobby=Blank(Native.GetType("MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby",true)!);
        object net=Blank(Native.GetType("MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService",true)!);
        Set(lobby,"<NetService>k__BackingField",net);Set(lobby,"<LobbyListener>k__BackingField",screen);
        Type playerType=Native.GetType("MegaCrit.Sts2.Core.Entities.Multiplayer.StartRunLobbyPlayer",true)!;
        object player=Activator.CreateInstance(playerType)!;Set(player,"id",1ul);Set(player,"character",ModelDb.Character<HbrgoSaki31O>());
        IList players=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(playerType))!;players.Add(player);Set(lobby,"<Players>k__BackingField",players);
        Set(screen,"_lobby",lobby);Set(screen,"_charSelectButtons",new List<NCharacterSelectButton>());Set(screen,"_charButtonContainer",Blank<Control>());
        Set(screen,"_embarkButton",Blank(Native.GetType("MegaCrit.Sts2.Core.Nodes.CommonUi.NConfirmButton",true)!));
        Set(screen,"_startRotationTickbox",Blank(Native.GetType("MegaCrit.Sts2.Core.Nodes.Screens.Dread.NDreadBeginRotationTickbox",true)!));
        Set(screen,"_chainVisualization",Blank(Native.GetType("MegaCrit.Sts2.Core.Nodes.Vfx.Ui.NChainVisualization",true)!));
        return screen;
    }
    static void Select(object screen,NCharacterSelectButton b)=>Call(screen,"SelectCharacter",b);
    static CharacterModel LobbyCharacter(object screen)=> (CharacterModel)Field(Native.GetType("MegaCrit.Sts2.Core.Entities.Multiplayer.StartRunLobbyPlayer",true)!,"character").GetValue(Get(screen,"_lobby")!.GetType().GetProperty("LocalPlayer")!.GetValue(Get(screen,"_lobby")))!;
    static void NativeNegative()
    {
        object screen=Screen();var b=InstantiateButton("");Call(b,"Init",ModelDb.Character<HbrgoSaki>(),screen);
        Check(b.IsLocked,"negative control: actual native Init plus real Ritsu scope locks hidden legacy base");Select(screen,b);
        Check(!Enabled[Get(screen,"_embarkButton")!],"negative control: real native Dread selection disables confirm for locked legacy base");
        Call(b,"Init",ModelDb.Character<HbrgoSaki31O>(),screen);Check(!b.IsLocked,"actual Init keeps visible31O unlocked");Select(screen,b);
        Check(Enabled[Get(screen,"_embarkButton")!]&&ReferenceEquals(LobbyCharacter(screen),ModelDb.Character<HbrgoSaki31O>()),"real native selection enables confirm and updates actual lobby to31O");
        Call(b,"Init",ModelDb.Character<HbrgoHotaru>(),screen);Check(!b.IsLocked,"actual Init keeps visible Hotaru unlocked");Select(screen,b);
        Check(Enabled[Get(screen,"_embarkButton")!]&&ReferenceEquals(LobbyCharacter(screen),ModelDb.Character<HbrgoHotaru>()),"Hotaru real native selection enables confirm and updates lobby");
    }
    static void ProductionCases()
    {
        Type[] patches=Mod.GetTypes().Where(t=>t.Name.StartsWith("Dread",StringComparison.Ordinal)&&t.GetCustomAttributes(typeof(HarmonyPatch),false).Length>0).ToArray();
        Check(patches.Length>0,"production has bounded Dread compatibility patch classes");
        foreach(Type t in patches)H.CreateClassProcessor(t).Patch();
        if(Dread==null){Check(patches.All(t=>t.GetMethod("Prepare",F)==null||(bool)Static(t,"Prepare")! == false),"stable without NDread safely skips optional targets");return;}
        object screen=Screen();Added.Clear();Call(screen,"InitCharacterButtons");
        var buttons=(List<NCharacterSelectButton>)Get(screen,"_charSelectButtons")!;
        var characters=buttons.Where(b=>b.Character!=null).Select(b=>b.Character).ToArray();
        Check(characters.Count(c=>c is HbrgoSaki31O)==1&&characters.Count(c=>c is HbrgoHotaru)==1,"real patched Dread Init lists one31O and oneHotaru");
        Check(!characters.Any(c=>c.GetType()==typeof(HbrgoSaki)||c is HbrgoSaki30A),"real patched Dread Init excludes hidden base and30A standalone buttons");
        Check(characters.Distinct(ReferenceEqualityComparer.Instance).Count()==characters.Length,"real native menu has no duplicated canonical character buttons");
        Check(buttons.Count==characters.Length+1,"native rotating button remains present separately");
        foreach(var b in buttons.Where(b=>b.Character is HbrgoSaki31O or HbrgoHotaru))
        {Check(!b.IsLocked,"fixed menu button is actually unlocked "+b.Character.GetType().Name);Select(screen,b);Check(Enabled[Get(screen,"_embarkButton")!]&&ReferenceEquals(LobbyCharacter(screen),b.Character),"fixed menu selection reaches native enabled confirm and exact lobby "+b.Character.GetType().Name);}
        VariantCases();
    }
    static void VariantCases()
    {
        object NewShared(out NCharacterSelectButton shared)
        {var screen=Screen();Call(screen,"InitCharacterButtons");shared=((List<NCharacterSelectButton>)Get(screen,"_charSelectButtons")!).Single(b=>b.Character is HbrgoSaki31O);return screen;}
        Preferred=HbrgoSakiVariant.ThirtyA;ThirtyAUnlocked=false;
        object locked=NewShared(out var lockedShared);Select(locked,lockedShared);
        Check(LobbyCharacter(locked) is HbrgoSaki31O&&lockedShared.Character is HbrgoSaki31O,"locked30A preference uses real31O lobby without separate30A button");
        ThirtyAUnlocked=true;object unlocked=NewShared(out var shared);Select(unlocked,shared);
        Check(LobbyCharacter(unlocked) is HbrgoSaki30A&&Enabled[Get(unlocked,"_embarkButton")!],"unlocked30A preference reaches actual native lobby and enabled confirm");
        Check(shared.Character is HbrgoSaki31O,"selection finally restores shared canonical31O button identity");
        Call(unlocked,"BeginRotationTickboxChanged",true);
        Check(LobbyCharacter(unlocked) is HbrgoSaki30A&&shared.Character is HbrgoSaki31O,"real rotation checkbox keeps30A lobby identity and restores shared31O button");
        FailVisual=true;bool threw=false;try{Select(unlocked,shared);}catch(InvalidOperationException e)when(e.Message=="controlled Dread background failure"){threw=true;}finally{FailVisual=false;}
        Check(threw&&shared.Character is HbrgoSaki31O,"production selection finalizer restores shared identity after native scene-body failure");
        Type helper=Mod.GetType("HBRGOSakiMod.Core.Patches.HbrgoDreadCharacterSelectCompatibility",true)!;
        var rotation=((IEnumerable<CharacterModel>)Static(helper,"RotationCharacters")!).ToArray();
        Check(!rotation.Any(c=>c.GetType()==typeof(HbrgoSaki)||c is HbrgoSaki30A)&&rotation.Count(c=>c is HbrgoHotaru)==1&&rotation.Count(c=>c is HbrgoSaki31O)==1,"rotation source excludes retired variants and duplicate Hotaru");
        Type ascension=Native.GetType("MegaCrit.Sts2.Core.Helpers.AscensionHelper",true)!;var method=ascension.GetMethod("GetRotatingCharacterIdForStreak",[typeof(ModelId),typeof(int)])!;
        var starting=ModelDb.GetId<HbrgoSaki30A>();
        Check(((ModelId)Invoke(method,null,starting,0)!)==starting,"real native rotation preserves30A as selected starting run");
        ModelId after=(ModelId)Invoke(method,null,starting,1)!;
        Check(rotation.Any(c=>c.Id==after)&&after!=starting,"next actual rotation normalizes30A to shared catalog slot before advancing");
        Check(((ModelId)Invoke(method,null,ModelDb.GetId<HbrgoSaki>(),1)!)==after,"retired base starting alias advances from the same shared31O rotation slot");
        Preferred=HbrgoSakiVariant.ThirtyA;ThirtyAUnlocked=true;
        object legacyOpen=NewShared(out var legacyShared);Set(Progress,"<PreferredDreadCharacter>k__BackingField",ModelDb.GetId<HbrgoSaki>());Call(legacyOpen,"OnSubmenuOpened");
        Check(ReferenceEquals(Get(legacyOpen,"_overriddenFocusedControl"),legacyShared)&&LobbyCharacter(legacyOpen) is HbrgoSaki31O&&legacyShared.Character is HbrgoSaki31O,"production open postfix repairs stale legacy preference through actual shared Select delegate");
        object nativeOpen=NewShared(out _);Set(Progress,"<PreferredDreadCharacter>k__BackingField",ModelDb.GetId<MegaCrit.Sts2.Core.Models.Characters.Ironclad>());Call(nativeOpen,"OnSubmenuOpened");
        Check(Get(nativeOpen,"_overriddenFocusedControl")==null&&Get(nativeOpen,"_selectedButton")==null,"production open postfix does not override vanilla preferred selection");
        Preferred=HbrgoSakiVariant.ThirtyOneO;ThirtyAUnlocked=false;
    }
    public static void Run(string path)
    {
        bool ok=false;string? error=null;
        try{Install();if(Dread!=null)NativeNegative();ProductionCases();ok=true;}catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);}
        string Sha(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,dreadPresent=Dread!=null,modSha256=Sha(Mod.Location),nativeSha256=Sha(Native.Location),runtimeProfile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real native Dread Init enumeration/production filter, real Ritsu selection scope and native Button.Init lock state, real Dread SelectCharacter and StartRunLobby character mutation. Godot scene creation, geometry/texture setters, button animation and unrelated progress UI are controlled leaves. No Godot engine/render, save/network or actual run launch."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Dread native checks");
    }
}
