using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class CrushYouLikeABugReplayPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("D9B4FF");
    public string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/moonlight_replay_power.tres";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    // Load-only adapter: every current card grants MoonlightReplayPower. Its
    // shared source selection keeps any old saved counter from double-firing.
    public override int ModifyCardPlayCount(CardModel card, Creature target, int playCount)
    {
        if (!MoonlightReplayPower.CanReplay(this, card)) return playCount;
        Flash();
        return playCount + 1;
    }

    public override Task AfterModifyingCardPlayCount(CardModel card)
    {
        return MoonlightReplayPower.CanReplay(this, card)
            ? PowerCmd.Decrement(this)
            : Task.CompletedTask;
    }
}
