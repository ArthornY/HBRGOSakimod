using System;
using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.PotionPools;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Scaffolding.Characters;

namespace HBRGOSakiMod.Core.Models.Characters;

[CustomID("HBRGO_SAKI")]
public class HbrgoSaki :
    CustomCharacterModel,
    IModCharacterVanillaSelectionPolicy
{
    public override string CustomVisualPath =>
        "res://scenes/creature_visuals/hbrgo_saki.tscn";

    public override string CustomTrailPath =>
        "res://scenes/vfx/card_trail_hbrgo_saki.tscn";

    public override string CustomIconTexturePath =>
        "res://images/ui/top_panel/character_icon_hbrgo_saki.png";

    public override string CustomIconOutlineTexturePath =>
        "res://images/ui/top_panel/character_icon_hbrgo_saki_outline.png";

    public override string CustomIconPath =>
        "res://scenes/ui/character_icons/hbrgo_saki_icon.tscn";

    public override string CustomEnergyCounterPath =>
        "res://scenes/combat/energy_counters/hbrgo_saki_energy_counter.tscn";

    public override string CustomRestSiteAnimPath =>
        "res://scenes/rest_site/characters/hbrgo_saki_rest_site.tscn";

    public override string CustomMerchantAnimPath =>
        "res://scenes/merchant/characters/hbrgo_saki_merchant.tscn";

    public override string CustomArmPointingTexturePath =>
        "res://images/ui/hands/multiplayer_hand_hbrgo_saki_point.png";

    public override string CustomArmRockTexturePath =>
        "res://images/ui/hands/multiplayer_hand_hbrgo_saki_rock.png";

    public override string CustomArmPaperTexturePath =>
        "res://images/ui/hands/multiplayer_hand_hbrgo_saki_paper.png";

    public override string CustomArmScissorsTexturePath =>
        "res://images/ui/hands/multiplayer_hand_hbrgo_saki_scissors.png";

    public override string CustomCharacterSelectBg =>
        "res://scenes/screens/char_select/char_select_bg_hbrgo_saki.tscn";

    public override string CustomCharacterSelectTransitionPath =>
        "res://materials/transitions/hbrgo_saki_transition_mat.tres";

    public override string CustomMapMarkerPath =>
        "res://images/packed/map/icons/map_marker_hbrgo_saki.png";

    public override Color NameColor => new("5C8DFFFF");

    public override CharacterGender Gender => CharacterGender.Feminine;

    public override bool HideFromVanillaCharacterSelect => true;

    public override bool AllowInVanillaRandomCharacterSelect => false;

    public override bool HideInCompendium => true;

    public virtual bool HideInCardLibraryCompendium => true;

    protected override CharacterModel UnlocksAfterRunAs => null;

    public override int StartingHp => HbrBattleStatsHelper.GetInitialPlayerHp(35);

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
        ModelDb.Relic<DeflectorProtection>(),
        ModelDb.Relic<Overclock>(),
        ModelDb.Relic<RestorationMoment>()
    };

    protected override string CharacterSelectIconPath => ImageHelper.GetImagePath("packed/character_select/sakikowin.png");

    protected override string CharacterSelectLockedIconPath => CharacterSelectIconPath;

    public override float AttackAnimDelay => 0.15f;

    public override float CastAnimDelay => 0.25f;

    public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";

    public override Color DialogueColor => new("1C274A");

    public override Color EnergyLabelOutlineColor => new("1C274AFF");

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
