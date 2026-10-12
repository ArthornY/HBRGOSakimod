using Godot;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NNararaShibaVisuals : NStaticHbrgoMonsterVisuals
{
    private const string SleepingBackgroundPath =
        "res://images/backgrounds/narara_shiba/sleeping.png";
    private const string AwakeBackgroundPath =
        "res://images/backgrounds/narara_shiba/awake.jpg";
    private const string WorkBackgroundPath =
        "res://images/backgrounds/narara_shiba/work.jpg";
    private const string OffWorkBackgroundPath =
        "res://images/backgrounds/narara_shiba/off_work.jpg";
    private const string UnwillingBackgroundPath =
        "res://images/backgrounds/narara_shiba/unwilling.jpg";

    private string _visualPath;

    public override void _Ready()
    {
        base._Ready();
        RefreshVisuals();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        var creature = (GetParent() as NCreature)?.Entity;
        if (creature?.Monster is not NararaShibaBossMonster boss)
        {
            return;
        }

        string path = GetBackgroundPath(boss);
        GetCurrentBody().Visible = true;
        if (_visualPath == path)
        {
            return;
        }

        Texture2D texture = ResourceLoader.Load<Texture2D>(path);
        if (texture == null)
        {
            return;
        }

        SetVisualTexture(texture);
        _visualPath = path;
    }

    private static string GetBackgroundPath(
        NararaShibaBossMonster boss)
    {
        if (boss.Creature.HasPower<NararaSleepingPower>())
        {
            return SleepingBackgroundPath;
        }

        return boss.NextMove?.Id switch
        {
            NararaShibaBossMonster.WorkMoveId => WorkBackgroundPath,
            NararaShibaBossMonster.OffWorkMoveId =>
                OffWorkBackgroundPath,
            NararaShibaBossMonster.DoNotWantToWorkMoveId =>
                UnwillingBackgroundPath,
            _ => AwakeBackgroundPath
        };
    }

}
