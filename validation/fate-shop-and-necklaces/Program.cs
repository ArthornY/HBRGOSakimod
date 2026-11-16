using System.Text.Json;
using HBRGOSakiMod.Core.FateDice;
using HBRGOSakiMod.Core.Equipment;

var passed = new List<string>();
void Check(bool ok, string label)
{
    if (!ok) throw new InvalidOperationException(label);
    passed.Add(label);
}

const string room = "act2:3:4";
HbrFateDiceState empty = HbrFateDiceState.Effective(HbrFateDiceState.Empty, 0);
HbrFateShopOffer[] offers =
[
    new(HbrFateDiceCatalog.WideEdge, 160, false),
    new(HbrFateDiceCatalog.Floor, 120, false)
];
HbrFateDiceState taken = HbrFateDiceCommands.StealShop(empty, room, offers);
Check(taken.Owns(offers[0].Fragment) && taken.Owns(offers[1].Fragment), "parasol takes every stocked fragment");
Check(taken.Equipped.Length == 2 && taken.Bag.Length == 0, "available slots receive the stolen fragments");
Check(offers.All(o => taken.HasReceipt(HbrFateDiceState.ShopReceipt(room, o.Fragment))), "each stolen fragment records the normal purchase receipt");
Check(ReferenceEquals(taken, HbrFateDiceCommands.StealShop(taken, room, offers)), "room replay cannot duplicate a haul");
HbrFateDiceState restored = HbrFateDiceState.Parse(taken.Serialize());
Check(ReferenceEquals(restored, HbrFateDiceCommands.StealShop(restored, room, offers)), "saved receipts prevent duplicate fragments after reload");
var closed = empty.WithCrack().WithCrack();
var bagged = HbrFateDiceCommands.StealShop(closed, room, offers);
Check(bagged.Equipped.Length == 0 && bagged.Bag.Length == 2, "a full or cracked loadout keeps stolen fragments in the bag");
var sold = offers.Select(o => o with { Sold = true }).ToArray();
Check(ReferenceEquals(empty, HbrFateDiceCommands.StealShop(empty, room, sold)), "out-of-stock fragments are not taken");
Check(ReferenceEquals(empty, HbrFateDiceCommands.StealShop(empty, null, offers)), "no merchant room means no haul");
var buy = new HbrFateDiceCommand { Op = HbrFateDiceOp.Buy, Fragment = offers[0].Fragment, ShopKey = room };
Check(!HbrFateDiceCommands.Evaluate(empty, buy, true, 0, room, offers).Succeeded, "ordinary buying still requires gold");
Check(!HbrFateDiceCommands.Evaluate(empty, buy, true, 160, "another-room", offers).Succeeded, "ordinary buying still checks the current shop");
var paid = HbrFateDiceCommands.Evaluate(empty, buy, true, 160, room, offers);
Check(paid.Succeeded && paid.GoldCost == 160, "ordinary purchase cost is unchanged");
Check(!HbrFateDiceCommands.Evaluate(taken, buy, true, 160, room, offers).Succeeded, "stolen stock cannot be bought again");

decimal Energy(string id) => HbrEquipmentRules.ItemEffects(new HbrEquipmentItem { Template = id })
    .Where(effect => effect.Effect == HbrEquipmentEffect.EnergyPerTurn).Sum(effect => effect.Value);
Check(Energy("Acc.Necklace005e") == 1m, "five-star destiny necklace grants one energy");
for (int i = 1; i <= 6; i++)
    Check(Energy($"Acc.Necklace006.F{i:000}") == 2m, $"six-star chain F{i:000} grants two energy");
Check(Energy("Acc.Necklace006.F007") == 1m, "ancient chains retain their separate energy-and-buff design");
var keptItem = new HbrEquipmentItem { Template = "Acc.Necklace006.F003" };
var keptJson = JsonSerializer.Serialize(keptItem);
var reloadedItem = JsonSerializer.Deserialize<HbrEquipmentItem>(keptJson);
Check(HbrEquipmentRules.ItemEffects(reloadedItem).Any(e => e.Effect == HbrEquipmentEffect.EnergyPerTurn && e.Value == 2m), "already saved six-star necklaces use the new template effect");
Check(HbrEquipmentCatalog.Find("Acc.Necklace006.F003").Rarity == 6, "the upgrade does not change necklace identity or rarity");

string report = args.Length > 0 ? args[0] : "fate-shop-and-necklaces-report.json";
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
File.WriteAllText(report, JsonSerializer.Serialize(new { passed = passed.Count, checks = passed,
    scope = "Pure production transaction and catalog rules; no Godot scene, merchant animation or native room hook executed." },
    new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Passed {passed.Count} fate shop and necklace checks.");
