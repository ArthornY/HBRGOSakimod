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
    private const string RainbowShaderPath = "res://shaders/hotaru_energy_glow.gdshader";
    private const string GlowShaderPath = "res://shaders/hbrgo_saki_energy_glow.gdshader";

    private static readonly AccessTools.FieldRef<NEnergyCounter, Player> PlayerField =
        AccessTools.FieldRefAccess<NEnergyCounter, Player>("_player");

    private TextureRect _icon;
    private TextureRect _glow;
    private string _iconPath;
    private ShaderMaterial _rainbow;
    private ShaderMaterial _sakura;
    private ShaderMaterial _soulFlame;

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

    // Each icon's lit part (31O's sakura accents, 30A's soul flame, Hotaru's rainbow rim)
    // is a matching *_glow.png drawn by a self-lit shader on %Glow. %Glow sits outside
    // %Layers because the base counter resets the materials of everything under %Layers,
    // and it goes out at zero energy, when the base counter darkens the orb.
    private void RefreshIcon()
    {
        if (_icon == null || _glow == null)
        {
            return;
        }

        Player player = PlayerField(this);
        string path = GetIconPath(player);
        if (path != _iconPath)
        {
            _iconPath = path;
            _icon.Texture = ResourceLoader.Load<Texture2D>(path);
            _glow.Texture = ResourceLoader.Load<Texture2D>(path.Replace(".png", "_glow.png"));
            _glow.Material = GetGlowMaterial(path);
        }

        _glow.Visible = player?.PlayerCombatState?.Energy > 0;
    }

    private ShaderMaterial GetGlowMaterial(string iconPath) => iconPath switch
    {
        HotaruIconPath => _rainbow ??= CreateGlow(RainbowShaderPath),
        ThirtyAIconPath => _soulFlame ??= CreateGlow(GlowShaderPath, ("flicker_amount", 0.45f), ("intensity", 1.05f)),
        _ => _sakura ??= CreateGlow(GlowShaderPath, ("pulse_amount", 0.18f))
    };

    private static ShaderMaterial CreateGlow(string shaderPath, params (string Name, float Value)[] parameters)
    {
        var material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(shaderPath) };
        foreach ((string name, float value) in parameters)
        {
            material.SetShaderParameter(name, value);
        }

        return material;
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
