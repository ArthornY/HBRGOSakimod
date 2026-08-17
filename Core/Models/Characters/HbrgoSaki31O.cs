using System.Collections.Generic;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Characters;

namespace HBRGOSakiMod.Core.Models.Characters;

[CustomID("HBRGO_SAKI_31O")]
public sealed class HbrgoSaki31O : HbrgoSaki, IModCharacterVanillaSelectionPolicy
{
    public override IReadOnlyList<RelicModel> StartingRelics =>
        new RelicModel[]
        {
            ModelDb.Relic<ElectronicSoldierManual>(),
            ModelDb.Relic<SoulOfAnchor>(),
            ModelDb.Relic<DeflectorProtection>()
        };

    public override bool HideFromVanillaCharacterSelect => true;

    public override bool AllowInVanillaRandomCharacterSelect => false;

    public override bool HideInCompendium => true;

    public bool HideInCardLibraryCompendium => true;
}
