using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NMemoryGardenCompendiumSkillsButton : Button
{
    private const string NodeName = "MemoryGardenCompendiumSkillsButton";
    private CardModel _card;
    private NInspectCardScreen _inspect;
    private Control _upgradeLabel;

    internal static void Refresh(NInspectCardScreen inspect, CardModel card)
    {
        var button = inspect.GetNodeOrNull<NMemoryGardenCompendiumSkillsButton>(NodeName);
        if (button == null)
        {
            if (!HbrMemoryGardenCompendiumHelper.CanBrowseSkills(card)) return;
            button = new NMemoryGardenCompendiumSkillsButton
            {
                Name = NodeName,
                _inspect = inspect,
                _upgradeLabel = inspect.GetNodeOrNull<Control>("%ShowUpgradeLabel")
            };
            inspect.AddChild(button);
        }
        button._card = card;
        button.Visible = HbrMemoryGardenCompendiumHelper.CanBrowseSkills(card);
        if (button.Visible) button.LayoutButton();
    }

    public override void _Ready()
    {
        Text = NMemoryGardenUpgradeSkillsScreen.Text("all_upgrade_skills");
        AddThemeFontSizeOverride("font_size", 18);
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        Pressed += () => NMemoryGardenUpgradeSkillsScreen.Show(_card, this);
    }

    public override void _Process(double delta)
    {
        Visible = HbrMemoryGardenCompendiumHelper.CanBrowseSkills(_card);
        if (Visible) LayoutButton();
    }

    private void LayoutButton()
    {
        Vector2 viewport = _inspect.Size;
        if (viewport.X <= 0 || viewport.Y <= 0) viewport = GetViewportRect().Size;
        Size = new Vector2(Math.Min(240, Math.Max(1, viewport.X - 32)), 42);
        Vector2 position = new((viewport.X - Size.X) / 2, viewport.Y - 106);
        if (GodotObject.IsInstanceValid(_upgradeLabel) && _upgradeLabel.IsInsideTree())
        {
            Rect2 rect = _upgradeLabel.GetGlobalRect();
            Transform2D inverse = _inspect.GetGlobalTransform().AffineInverse();
            Vector2 start = inverse * rect.Position;
            Vector2 end = inverse * rect.End;
            position = new Vector2(end.X + 24, start.Y + (end.Y - start.Y - Size.Y) / 2);
            if (position.X + Size.X > viewport.X - 16)
                position = new Vector2(start.X, end.Y + 10);
        }
        Position = new Vector2(Math.Clamp(position.X, 16, Math.Max(16, viewport.X - Size.X - 16)),
            Math.Clamp(position.Y, 16, Math.Max(16, viewport.Y - Size.Y - 16)));
    }
}
