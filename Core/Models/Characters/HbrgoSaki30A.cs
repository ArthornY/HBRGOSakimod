using System.Collections.Generic;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Characters;

namespace HBRGOSakiMod.Core.Models.Characters;

[CustomID("HBRGO_SAKI_30A")]
public sealed class HbrgoSaki30A : HbrgoSaki, IModCharacterVanillaSelectionPolicy
{
    public override CardPoolModel CardPool =>
        ModelDb.CardPool<HBRGOSaki30ACardPool>();

    public override string CustomCharacterSelectBg =>
        "res://scenes/screens/char_select/char_select_bg_hbrgo_saki_30a.tscn";

    public override int StartingHp => 10;

    public override IEnumerable<CardModel> StartingDeck => new CardModel[]
    {
        ModelDb.Card<StrikeHbrgoSaki>(),
        ModelDb.Card<StrikeHbrgoSaki>(),
        ModelDb.Card<StrikeHbrgoSaki>(),
        ModelDb.Card<StrikeHbrgoSaki>(),
        ModelDb.Card<DefendHbrgoSaki>(),
        ModelDb.Card<DefendHbrgoSaki>(),
        ModelDb.Card<DefendHbrgoSaki>(),
        ModelDb.Card<DefendHbrgoSaki>(),
        ModelDb.Card<StrengthAmplificationHbrgoSaki>(),
        ModelDb.Card<CommandStrikeHbrgoSaki>()
    };

    public override IReadOnlyList<RelicModel> StartingRelics =>
        new RelicModel[]
        {
            ModelDb.Relic<ElectronicSoldierManual>(),
            ModelDb.Relic<MomentOfReign>(),
            ModelDb.Relic<DeflectorProtection>(),
            ModelDb.Relic<Overclock>()
        };

    public override bool HideFromVanillaCharacterSelect => true;

    public override bool AllowInVanillaRandomCharacterSelect => false;

    public override bool HideInCompendium => true;

    public override bool HideInCardLibraryCompendium => true;
}
