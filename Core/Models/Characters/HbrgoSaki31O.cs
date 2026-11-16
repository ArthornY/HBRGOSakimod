using System.Collections.Generic;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.PotionPools;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Characters;

namespace HBRGOSakiMod.Core.Models.Characters;

[CustomID("HBRGO_SAKI_31O")]
public sealed class HbrgoSaki31O : HbrgoSaki, IModCharacterVanillaSelectionPolicy
{
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<HBRGOSaki31ORelicPool>();

    public override CardPoolModel CardPool =>
        ModelDb.CardPool<HBRGOSaki31OCardPool>();

    public override PotionPoolModel PotionPool =>
        ModelDb.PotionPool<HBRGOSaki31OPotionPool>();

    public override IReadOnlyList<RelicModel> StartingRelics =>
        new RelicModel[]
        {
            ModelDb.Relic<ElectronicSoldierManual31O>(),
            ModelDb.Relic<SoulOfAnchor>(),
            ModelDb.Relic<DeflectorProtection>(),
            ModelDb.Relic<Overclock>()
        };

    public override bool HideFromVanillaCharacterSelect => false;

    public override bool AllowInVanillaRandomCharacterSelect => true;

    public override bool HideInCompendium => false;

    public override bool HideInCardLibraryCompendium => false;
}
