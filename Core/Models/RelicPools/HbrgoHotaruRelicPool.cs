using BaseLib.Abstracts;
using Godot;

namespace HBRGOSakiMod.Core.Models.RelicPools;

// Universal relics are supplied by the game's shared pool. This pool registers
// Hotaru's starter relics without adding them to ordinary random rewards.
public sealed class HbrgoHotaruRelicPool : CustomRelicPoolModel
{
    public override string EnergyColorName => "hbrgosaki";
    public override Color LabOutlineColor => new("D4D6EEFF");
}
