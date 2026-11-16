using System.Security.Cryptography;
using System.Text.Json;
using HBRGOSakiMod.Core.Nodes.Combat;
using Mono.Cecil;
using Mono.Cecil.Cil;

// No Godot objects are constructed. The production state is linked unchanged;
// the optional DLL checks verify its actual compiled node callback connections.
int checks = 0;
List<string> passed = new();
void Check(bool condition, string description)
{
    checks++;
    if (!condition) throw new InvalidOperationException(description);
    passed.Add(description);
}

HbrMagicSwordHoverState state = new();
Check(!state.WantsTooltip, "unfocused idle has no tooltip owner");
state.PointerEntered();
Check(state.WantsTooltip, "pointer entry owns the tooltip");
state.SetFocused(true);
Check(state.WantsTooltip && state.Focused, "mouse click may keep real Godot focus");
state.PointerExited();
Check(!state.WantsTooltip && state.Focused, "mouse exit closes even though click focus remains");
for (int turn = 0; turn < 3; turn++)
    Check(!state.WantsTooltip, "later normal/extra turn or grey-state refresh cannot revive click focus " + turn);
Check(!state.NavigationInput("Shift", true) && !state.WantsTooltip, "Shift detail key cannot reopen a tooltip through leftover click focus");
Check(!state.NavigationInput("A", true) && !state.WantsTooltip, "unrelated letter key cannot reopen the tooltip");
state.NavigationInput("ui_accept", state.Focused);
Check(state.WantsTooltip, "keyboard/gamepad can reclaim existing focus");
state.SetFocused(false);
Check(!state.WantsTooltip, "navigation focus exit closes the navigation-owned tooltip");
state.NavigationInput("ui_accept", state.Focused);
state.SetFocused(true);
Check(state.WantsTooltip, "new keyboard/gamepad focus opens tooltip");
state.UsePointer();
Check(!state.WantsTooltip && state.Focused, "switching to mouse away from button retires navigation tooltip without releasing focus");
state.PointerEntered();
state.SetFocused(false);
Check(state.WantsTooltip, "focus exit leaves an active pointer tooltip intact");
state.PointerExited();
Check(!state.WantsTooltip, "pointer tooltip closes when pointer leaves");
state.Reset();
state.NavigationInput("ui_accept", state.Focused);
state.SetFocused(true);
state.PointerEntered();
state.PointerExited();
Check(!state.WantsTooltip && state.Focused, "pointer takeover prevents an older keyboard focus surviving mouse exit");
state.Reset();
Check(!state.WantsTooltip && !state.PointerInside && !state.Focused, "hidden/tree exit clears all tooltip ownership");
state.SetFocused(true);
Check(!state.WantsTooltip, "stale focus signal after hide cannot recreate a tooltip");
state.NavigationInput("ui_accept", state.Focused);
Check(state.WantsTooltip, "fresh navigation after showing works again");
state.Reset();
state.PointerEntered();
Check(state.WantsTooltip, "disabled button still explains why it cannot open on real hover");
state.PointerExited();
Check(!state.WantsTooltip, "disabled button explanation never becomes sticky");

string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
string statePath = Path.Combine(repo, "Core/Nodes/Combat/HbrMagicSwordHoverState.cs");
string nodePath = Path.Combine(repo, "Core/Nodes/Combat/NHbrMagicSwordButton.cs");
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
string? candidate = args.Length > 0 && args[0] != "-" ? Path.GetFullPath(args[0]) : null;
if (candidate != null)
{
    if (args.Length > 1 && args[1] != "-") Check(Hash(candidate) == args[1].ToUpperInvariant(), "compiled candidate exact SHA binding");
    using ModuleDefinition module = ModuleDefinition.ReadModule(candidate);
    TypeDefinition node = module.GetType("HBRGOSakiMod.Core.Nodes.Combat.NHbrMagicSwordButton");
    MethodDefinition Method(string name) => node.Methods.Single(m => m.Name == name);
    IEnumerable<MethodReference> Calls(string name) => Method(name).Body.Instructions
        .Where(i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt || i.OpCode == OpCodes.Ldftn)
        .Select(i => i.Operand).OfType<MethodReference>();
    bool CallsNamed(string method, string name) => Calls(method).Any(m => m.Name == name);
    bool CallsState(string method, string name) => Calls(method).Any(m => m.DeclaringType.Name == "HbrMagicSwordHoverState" && m.Name == name);
    Check(module.GetType("HBRGOSakiMod.Core.Nodes.Combat.HbrMagicSwordHoverState") != null, "candidate includes the production ownership state");
    Check(CallsNamed("_Ready", "OnFocusEntered") && CallsNamed("_Ready", "OnFocusExited"), "real focus signals connect ownership callbacks");
    Check(CallsNamed("_Ready", "OnMouseEntered") && CallsNamed("_Ready", "OnMouseExited"), "real pointer signals connect ownership callbacks");
    Check(CallsNamed("_ExitTree", "OnFocusEntered") && CallsNamed("_ExitTree", "OnFocusExited") && CallsState("_ExitTree", "Reset") && CallsNamed("_ExitTree", "HideHoverTip"), "tree exit disconnects focus and clears owned tips");
    Check(CallsState("OnMouseEntered", "PointerEntered") && CallsNamed("OnMouseEntered", "RefreshHoverTip"), "pointer entry uses the tested production state");
    Check(CallsState("OnMouseExited", "PointerExited") && CallsNamed("OnMouseExited", "RefreshHoverTip") && !CallsNamed("OnMouseExited", "HasFocus"), "compiled pointer exit cannot retain tip through click focus");
    Check(CallsState("OnFocusEntered", "SetFocused") && CallsNamed("OnFocusEntered", "RefreshHoverTip"), "focus entry uses ownership-aware refresh");
    Check(CallsState("OnFocusExited", "SetFocused") && CallsNamed("OnFocusExited", "RefreshHoverTip") && !CallsNamed("OnFocusExited", "HideHoverTip"), "focus exit preserves current pointer ownership");
    Check(CallsState("_Input", "UsePointer") && CallsState("_Input", "NavigationInput") && CallsNamed("_Input", "HasFocus"), "actual input callback distinguishes mouse from keyboard/controller without removing focus");
    string[] eventTypes = Method("_Input").Body.Instructions.Select(i => i.Operand).OfType<TypeReference>().Select(t => t.Name).Distinct().ToArray();
    Check(new[] { "InputEventMouseButton", "InputEventMouseMotion" }.All(eventTypes.Contains) && CallsNamed("_Input", "IsActionPressed"), "compiled input handles native keyboard/gamepad navigation actions and pointer events");
    Check(CallsState("RefreshHoverTip", "get_WantsTooltip") && !CallsNamed("RefreshHoverTip", "HasFocus") && CallsNamed("RefreshHoverTip", "HideHoverTip"), "frame refresh closes unowned tip instead of treating persistent focus as ownership");
    Check(CallsNamed("RefreshHoverTip", "IsVisibleInTree") && Method("RefreshHoverTip").Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "shouldBlockHoverTips"), "frame refresh hides when ancestor is hidden or game blocks hover tips");
    Check(CallsNamed("RefreshHoverTip", "op_Inequality") && CallsNamed("RefreshHoverTip", "ShowHoverTip") && CallsNamed("RefreshHoverTip", "IsInstanceValid"), "valid unchanged tooltip is reused rather than rebuilt every frame");
    Check(CallsState("_Process", "Reset") && CallsNamed("_Process", "HideHoverTip"), "combat hidden branch clears ownership and tooltip");
    Check(CallsNamed("_GuiInput", "HasFocus") && CallsNamed("_GuiInput", "Press") && CallsNamed("_Process", "LinkToHand"), "keyboard accept and existing hand navigation remain wired");
}

var report = new
{
    ok = true,
    checks,
    candidate,
    candidateSha256 = candidate == null ? null : Hash(candidate),
    sourceHashes = new Dictionary<string, string> { ["Core/Nodes/Combat/HbrMagicSwordHoverState.cs"] = Hash(statePath), ["Core/Nodes/Combat/NHbrMagicSwordButton.cs"] = Hash(nodePath) },
    mode = candidate == null ? "production-linked state" : "production-linked state and actual candidate callback IL",
    godotStarted = false,
    actualGodotSignalsOrRenderingExercised = false,
    limitation = "No engine: OS pointer geometry, actual Godot signal ordering and rendered tooltip position need in-game verification.",
    passed
};
string output = args.Length > 2 ? Path.GetFullPath(args[2]) : Path.Combine(repo, "outputs/magic-hover-elite-load-20261115/hover-state-report.json");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine(JsonSerializer.Serialize(new { ok = true, checks, report = output, candidateSha256 = report.candidateSha256 }));
