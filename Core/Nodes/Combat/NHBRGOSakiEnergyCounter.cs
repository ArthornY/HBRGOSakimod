using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NHBRGOSakiEnergyCounter : NEnergyCounter
{
    private const string NormalIconPath =
        "res://images/ui/energys/hbrgosaki_energy_counter.png";
    private const string BowIconPath =
        "res://images/ui/energys/hbrgosaki_energy_counter_bow.png";
    private const string DualSwordsIconPath =
        "res://images/ui/energys/hbrgosaki_energy_counter_dual_swords.png";
    private const string ThirtyAIconPath =
        "res://images/ui/energys/hbrgosaki30a_energy_counter.png";
    private const string HotaruIconPath =
        "res://images/ui/energys/hbrgohotaru_energy_counter.png";
    private const string HotaruGlowPath =
        "res://images/ui/energys/hbrgohotaru_energy_counter_glow.png";

    private static readonly AccessTools.FieldRef<NEnergyCounter, Player> PlayerField =
        AccessTools.FieldRefAccess<NEnergyCounter, Player>("_player");

    private TextureRect _icon;
    private TextureRect _glow;
    private string _iconPath;

    public override void _Ready()
    {
        base._Ready();
        _icon = GetNode<TextureRect>("%Layers/Layer1");
        _glow = GetNode<TextureRect>("%Glow");
        RefreshIcon();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        RefreshIcon();
    }

    private void RefreshIcon()
    {
        Player player = PlayerField(this);
        RefreshGlow(player);
        string path = GetIconPath(player);
        if (_icon == null || path == _iconPath)
        {
            return;
        }

        _iconPath = path;
        _icon.Texture = ResourceLoader.Load<Texture2D>(path);
    }

    // Hotaru's rainbow rim is a self-lit shader layer outside %Layers, because the
    // base counter resets the materials of everything under %Layers. It goes out at
    // zero energy, when the base counter darkens the orb.
    private void RefreshGlow(Player player)
    {
        bool lit = player?.Character is HbrgoHotaru && player.PlayerCombatState?.Energy > 0;
        if (_glow == null || _glow.Visible == lit)
        {
            return;
        }

        _glow.Texture ??= ResourceLoader.Load<Texture2D>(HotaruGlowPath);
        _glow.Visible = lit;
    }

    // Hotaru shares this scene. 31O follows her combat portrait; the scythe borrows 30A's crescent.
    private static string GetIconPath(Player player) => player?.Character switch
    {
        HbrgoHotaru => HotaruIconPath,
        HbrgoSaki30A => ThirtyAIconPath,
        _ => HbrgoSakiCombatFormHelper.GetForm(player?.Creature) switch
        {
            HbrgoSakiCombatForm.Scythe => ThirtyAIconPath,
            HbrgoSakiCombatForm.Bow => BowIconPath,
            HbrgoSakiCombatForm.DualSwords => DualSwordsIconPath,
            _ => NormalIconPath
        }
    };
}
