using System.Text.Json;
using HBRGOSakiMod.Core.Nodes.Screens;

string repo = Path.GetFullPath(args[0]);
string report = Path.GetFullPath(args[1]);
List<string> checks = [];
void Check(bool condition, string message)
{ if (!condition) throw new Exception(message); checks.Add(message); }
foreach ((int original, int final, bool discount) in new[]
{ (100, 100, false), (100, 90, true), (101, 36, true), (1, 1, false), (0, 0, false), (100, 120, false) })
{
    HbrManualShopPriceView view = HbrManualShopPriceView.From(original, final);
    Check(view.Original == original && view.Final == final, "presentation retains the service's exact quote without recalculating a discount");
    Check(view.Discounted == discount, "only a lower quoted price displays a discount");
    Check(discount ? view.OriginalBbCode == $"[s]{original}[/s]" && view.Badge == "佐月的优惠" :
        view.OriginalBbCode == string.Empty && view.Badge == string.Empty, "strike markup and Satsuki badge appear together only for a real discount");
}
foreach (float width in new[] { 320f, 480f, 759f, 760f, 884f, 1100f })
foreach (float height in new[] { 360f, 720f, 1180f })
{
    HbrManualShopComparisonLayout view = HbrManualShopPresentation.Comparison(width, height);
    HbrPhoneRect panel = view.Panel;
    float left = panel.X + HbrManualShopPresentation.ComparisonInnerMargin;
    float right = left + view.ColumnWidth + HbrManualShopPresentation.ComparisonGap;
    Check(panel.X >= 0f && panel.Y >= 0f && panel.Right <= width && panel.Bottom <= height,
        $"comparison panel stays inside the phone page at{width}x{height}");
    Check(view.ColumnWidth >= 119f && view.TextWidth >= 95f,
        $"both equipment cards retain readable column widths at{width}x{height}");
    Check(left + view.ColumnWidth < right && right + view.ColumnWidth <= panel.Right - HbrManualShopPresentation.ComparisonInnerMargin,
        $"two candidates remain side by side without overlapping or leaving the panel at{width}x{height}");
    Check(Math.Abs(view.TextWidth + HbrManualShopPresentation.ComparisonCardMargin * 2f - view.ColumnWidth) < .01f,
        $"wrapped item text respects the actual card's horizontal padding at{width}x{height}");
}
Check(HbrManualShopPresentation.CompactOffers(320) && HbrManualShopPresentation.CompactOffers(760) &&
    !HbrManualShopPresentation.CompactOffers(884), "the existing narrow shop list moves its price/action below item text when needed");
string panelSource = File.ReadAllText(Path.Combine(repo, "Core/Nodes/Screens/NManualShopPanel.cs"));
string comparison = File.ReadAllText(Path.Combine(repo, "Core/Nodes/Screens/NManualShopPanel.Reforge.cs"));
Check(comparison.Contains("RichTextLabel price = new()") && comparison.Contains("BbcodeEnabled = true, Text = view.OriginalBbCode"),
    "original-price strike tags are sent to a BBCode-enabled RichTextLabel, not a plain Label");
Check(panelSource.Contains("gold >= quote.Price") && panelSource.Contains("originalPrice: quote.OriginalPrice") &&
    panelSource.Contains("HbrSatsukiShopPolicy.Get(_manual.Owner), _manual.Owner?.RunState.CurrentActIndex ?? -1"),
    "display affordability and request preflight use the same service quote/policy/raw-act inputs");
Check(panelSource.Contains("_shopPolicy.CacheKey") && panelSource.Contains("|{_shopAct}"),
    "changing passive upgrades, party kinds or raw act invalidates displayed prices even without an equipment revision");
Check(comparison.Contains("HbrEquipmentOp.Reforge, TransactionId = transaction.Id") &&
    comparison.Contains("HbrEquipmentOp.ChooseReforge, TransactionId = transaction.Id, Index = index"),
    "reforge and both keep choices are authoritative revision/transaction network commands");
Check(comparison.Contains("state.LastAlchemy") && comparison.Contains("transaction?.ChoicePending == true") &&
    !comparison.Contains("RandomCounter") && !comparison.Contains("RollAccessory") && !comparison.Contains("AddItem("),
    "opening and rebuilding the comparison reads persisted candidates without generating, charging or granting an item");
Check(comparison.Contains("body.AddChild(footer)") && comparison.Contains("_comparisonActions.AddChild(button)") &&
    !comparison.Contains("body.AddChild(button)"), "selection buttons remain outside the long equipment-details scroll area");
Check(comparison.Contains("left.FocusNeighborRight = left.FocusNext") && comparison.Contains("right.FocusNeighborLeft = right.FocusPrevious") &&
    comparison.Contains("FocusNeighborTop = _comparisonSecond.FocusNeighborTop = bar.GetPath()"),
    "native keyboard/controller navigation connects both choice buttons and the item-detail scrollbar");
Check(comparison.Contains("HbrEquipmentText.ItemText(item)") && comparison.Contains("HbrEquipmentText.PlusSlotsText(item)") &&
    comparison.Contains("HbrEquipmentArt.MakeItemIcon(template.Icon, template.Rarity"),
    "comparison uses the existing equipment page's real icon/name/stats/plus-slot presentation");
Check(panelSource.Contains("button.Disabled = comparing") && panelSource.Contains("editable && !comparing"),
    "pending comparison prevents mouse or focus access to unrelated shop transactions");
Check(panelSource.Contains("!policy.Enabled && kind == HbrEquipmentKind.Ring") && panelSource.Contains("基础星级权重（强化前）") &&
    panelSource.Contains("kind == HbrEquipmentKind.Necklace") && panelSource.Contains("项链没有★2，缺档沿用相邻星级商品。"),
    "active Satsuki descriptions distinguish base odds, the inactive ring restriction and the original necklace's missing star tier");
Directory.CreateDirectory(Path.GetDirectoryName(report)!);
File.WriteAllText(report, JsonSerializer.Serialize(new { ok = true, checks = checks.Count, passed = checks,
    boundary = "Runs real pure presentation/layout functions and source-wiring checks. No Godot process or rendered input test." }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PASS {checks.Count} shop55 layout/wiring checks");
