using BaseLib.Abstracts;
using Godot;

namespace HBRGOSakiMod.Core.Models.RelicPools;

// Universal relics are supplied by the game's shared pool. Hotaru has no
// character-exclusive random relics in this first implementation.
public sealed class HbrgoHotaruRelicPool : CustomRelicPoolModel
{
    public override string EnergyColorName => "hbrgosaki";
    public override Color LabOutlineColor => new("D4D6EEFF");
}
