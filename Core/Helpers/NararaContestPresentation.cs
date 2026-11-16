using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Helpers;

// Presentation consumes no RNG and never delays a command or takes input focus.
internal static class NararaContestPresentation
{
    internal static void Show(Creature player, Creature boss, int playerRoll, int bossRoll, bool success)
    {
        ShowAt(player, $"1d100={playerRoll}\n{(success ? "判定成功" : "无效化")}", success);
        ShowAt(boss, $"1d100={bossRoll}", !success);
    }

    private static void ShowAt(Creature creature, string text, bool success)
    {
        if (TestMode.IsOn) return;
        NCombatRoom room = NCombatRoom.Instance;
        var node = room?.GetCreatureNode(creature);
        if (room?.Ui == null || node == null || !GodotObject.IsInstanceValid(node) || !node.IsInsideTree()) return;
        Vector2 viewport = node.GetCanvasTransform() * node.VfxSpawnPosition;
        Label label = new()
        {
            Text = text, MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            Size = new Vector2(300, 110), ZIndex = 100,
            Position = room.Ui.GetGlobalTransformWithCanvas().AffineInverse() * viewport - new Vector2(150, 155)
        };
        label.AddThemeFontSizeOverride("font_size", 30);
        label.AddThemeConstantOverride("outline_size", 6);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeColorOverride("font_color", success ? new Color("9DFFD2") : new Color("FFC7D0"));
        room.Ui.AddChild(label);
        Tween tween = label.CreateTween();
        tween.TweenInterval(1.5);
        tween.TweenProperty(label, "modulate:a", 0f, .35);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }
}
