using BaseLib.Abstracts;
using Godot;

namespace HBRGOSakiMod.Core.Models.CardPools;

// Separate ownership keeps these cards out of the characters' ordinary card pools.
// Combat effects may still create them explicitly, including the 30A combination.
public sealed class HBRGOSakiMemoryGardenCardPool : CustomCardPoolModel
{
    // BaseLib must include this standalone pool in ModelDb's complete catalog so
    // CardModel.Pool and the card library can resolve it. Reward guards keep it
    // separate from every character's ordinary acquisition pools.
    public override bool IsShared => true;

    public override string Title => "hbrgo_saki_memory_garden";

    public override string EnergyColorName => "hbrgosaki";

    public override string CardFrameMaterialPath => "card_frame_light_blue";

    public override Color DeckEntryCardColor => new("7799CCFF");

    public override Color EnergyOutlineColor => new("1C274AFF");

    public override bool IsColorless => false;
}
