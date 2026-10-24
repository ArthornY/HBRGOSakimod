using System;
using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Scaffolding.Characters;

namespace HBRGOSakiMod.Core.Models.Characters;

[CustomID("HBRGO_HOTARU")]
public sealed class HbrgoHotaru : CustomCharacterModel, IModCharacterVanillaSelectionPolicy
{
    internal const int InitialCurrentHp = 1;
    internal const int InitialMaxHp = 30;
    public override string CustomVisualPath => "res://scenes/creature_visuals/hbrgo_hotaru.tscn";
    public override string CustomTrailPath => "res://scenes/vfx/card_trail_hbrgo_saki.tscn";
    public override string CustomIconTexturePath => "res://images/characters/hbrgo_hotaru/hotaru_icon.png";
    public override string CustomIconOutlineTexturePath => CustomIconTexturePath;
    public override string CustomIconPath => "res://scenes/ui/character_icons/hbrgo_hotaru_icon.tscn";
    public override string CustomEnergyCounterPath => "res://scenes/combat/energy_counters/hbrgo_saki_energy_counter.tscn";
    public override string CustomRestSiteAnimPath => "res://scenes/rest_site/characters/hbrgo_hotaru_rest_site.tscn";
    public override string CustomMerchantAnimPath => "res://scenes/merchant/characters/hbrgo_hotaru_merchant.tscn";
    public override string CustomArmPointingTexturePath => "res://images/ui/hands/multiplayer_hand_hbrgo_saki_point.png";
    public override string CustomArmRockTexturePath => "res://images/ui/hands/multiplayer_hand_hbrgo_saki_rock.png";
    public override string CustomArmPaperTexturePath => "res://images/ui/hands/multiplayer_hand_hbrgo_saki_paper.png";
    public override string CustomArmScissorsTexturePath => "res://images/ui/hands/multiplayer_hand_hbrgo_saki_scissors.png";
    public override string CustomCharacterSelectBg => "res://scenes/screens/char_select/char_select_bg_hbrgo_hotaru.tscn";
    public override string CustomCharacterSelectTransitionPath => "res://materials/transitions/hbrgo_saki_transition_mat.tres";
    public override string CustomMapMarkerPath => CustomIconTexturePath;

    // Pets are spawned by Millsage after room preloading has finished. Include
    // their native asset sets in the run session so the first combat does not
    // synchronously load each cannon scene while the room is still concealed.
    protected override IEnumerable<string> ExtraAssetPaths => base.ExtraAssetPaths
        .Concat(new MonsterModel[]
        {
            ModelDb.Monster<HotaruHappinessCannon>(),
            ModelDb.Monster<HotaruCoffinCannon>(),
            ModelDb.Monster<HotaruContestCannon>(),
            ModelDb.Monster<HotaruLongingCannon>(),
            ModelDb.Monster<HotaruAbsolutionCannon>(),
            ModelDb.Monster<HotaruFinalCannon>()
        }.SelectMany(cannon => cannon.AssetPaths))
        .Distinct(StringComparer.Ordinal);

    public override Color NameColor => new("D4D6EEFF");
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override bool HideFromVanillaCharacterSelect => false;
    public override bool AllowInVanillaRandomCharacterSelect => true;
    public override bool HideInCompendium => false;
    public bool HideInCardLibraryCompendium => false;
    protected override CharacterModel UnlocksAfterRunAs => null;

    // Native CharacterModel uses StartingHp as the initial maximum as well.
    // The existing new-run initialization applies InitialCurrentHp separately.
    public override int StartingHp => InitialMaxHp;
    public override int StartingGold => 99;
    public override CardPoolModel CardPool => ModelDb.CardPool<HbrgoHotaruCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<HbrgoHotaruRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<SharedPotionPool>();

    public override IEnumerable<CardModel> StartingDeck => new CardModel[]
    {
        ModelDb.Card<HotaruStrike>(), ModelDb.Card<HotaruStrike>(),
        ModelDb.Card<HotaruStrike>(), ModelDb.Card<HotaruStrike>(),
        ModelDb.Card<HotaruDefend>(), ModelDb.Card<HotaruDefend>(),
        ModelDb.Card<HotaruDefend>(), ModelDb.Card<HotaruDefend>(),
        ModelDb.Card<HotaruStimulate>(), ModelDb.Card<HotaruCharge>()
    };

    public override IReadOnlyList<RelicModel> StartingRelics => new RelicModel[]
    {
        ModelDb.Relic<HotaruElectronicSoldierManual>(),
        ModelDb.Relic<SoulOfAnchor>(),
        ModelDb.Relic<Millsage>(),
        ModelDb.Relic<HotaruIridescence>(),
        ModelDb.Relic<DeflectorProtection>(),
        ModelDb.Relic<Overclock>()
    };

    // Selection and map icons require an imported CompressedTexture2D.
    protected override string CharacterSelectIconPath => CustomIconTexturePath;
    protected override string CharacterSelectLockedIconPath => CharacterSelectIconPath;
    public override float AttackAnimDelay => 0.15f;
    public override float CastAnimDelay => 0.25f;
    public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";
    public override Color DialogueColor => new("494959");
    public override VfxColor SpeechBubbleColor => VfxColor.Cyan;
    public override Color MapDrawingColor => new("D4D6EE");
    public override Color RemoteTargetingLineColor => new("D4D6EEFF");
    public override Color RemoteTargetingLineOutline => new("494959FF");
    public override List<string> GetArchitectAttackVfx() => new()
    {
        "vfx/vfx_attack_slash", "vfx/vfx_flying_slash"
    };
}
