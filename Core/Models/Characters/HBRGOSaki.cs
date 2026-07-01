using System;
using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.PotionPools;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace HBRGOSakiMod.Core.Models.Characters;

public sealed class HbrgoSaki : CharacterModel
{
    public override Color NameColor => new("5C8DFFFF");

    public override CharacterGender Gender => CharacterGender.Feminine;

    protected override CharacterModel UnlocksAfterRunAs => null;

    public override int StartingHp => 35;

    public override int StartingGold => 99;

    public override CardPoolModel CardPool => ModelDb.CardPool<HBRGOSakiCardPool>();

    public override RelicPoolModel RelicPool => ModelDb.RelicPool<HBRGOSakiRelicPool>();

    public override PotionPoolModel PotionPool => ModelDb.PotionPool<HBRGOSakiPotionPool>();

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
        ModelDb.Card<MagicSwordLuckySevenHbrgoSaki>(),
        ModelDb.Card<ChihayaClassmateHbrgoSaki>()
    };

    public override IReadOnlyList<RelicModel> StartingRelics => new RelicModel[]
    {
        ModelDb.Relic<ElectronicSoldierManual>(),
        ModelDb.Relic<SoulOfAnchor>(),
        ModelDb.Relic<DeflectorProtection>()
    };

    protected override string CharacterSelectIconPath => ImageHelper.GetImagePath("packed/character_select/sakikowin.png");

    protected override string CharacterSelectLockedIconPath => CharacterSelectIconPath;

    public override float AttackAnimDelay => 0.15f;

    public override float CastAnimDelay => 0.25f;

    public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";

    public override Color DialogueColor => new("1C274A");

    public override VfxColor SpeechBubbleColor => VfxColor.Cyan;

    public override Color MapDrawingColor => new("5C8DFF");

    public override Color RemoteTargetingLineColor => new("5C8DFFFF");

    public override Color RemoteTargetingLineOutline => new("1C274AFF");

    public override List<string> GetArchitectAttackVfx()
    {
        return new List<string>
        {
            "vfx/vfx_attack_slash",
            "vfx/vfx_flying_slash"
        };
    }
}
