using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class MoonlightReplayPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("D9B4FF");

    public string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/moonlight_replay_power.tres";

    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    // The retired source may still exist in an older combat. Spend the new
    // counter first so two saved sources never add two replays to one card.
    private static PowerModel GetReplaySource(Creature owner)
    {
        MoonlightReplayPower moonlight = owner?.GetPower<MoonlightReplayPower>();
        if (moonlight?.Amount > 0) return moonlight;
        CrushYouLikeABugReplayPower legacy = owner?.GetPower<CrushYouLikeABugReplayPower>();
        return legacy?.Amount > 0 ? legacy : null;
    }

    internal static bool CanReplay(CardModel card) =>
        card?.IsMutable == true && card.Owner?.Creature is { } owner && GetReplaySource(owner) != null &&
        !CardPlayContextHelper.IsAutoPlay(card);

    internal static bool CanReplay(PowerModel source, CardModel card) =>
        source != null && card?.IsMutable == true && card.Owner?.Creature == source.Owner &&
        ReferenceEquals(GetReplaySource(source.Owner), source) &&
        !CardPlayContextHelper.IsAutoPlay(card);

    internal static int GetReplayCountBonus(CardModel card) => CanReplay(card) ? 1 : 0;

    public override int ModifyCardPlayCount(
        CardModel card,
        Creature target,
        int playCount)
    {
        if (!CanReplay(this, card))
        {
            return playCount;
        }

        Flash();
        return playCount + 1;
    }

    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (!CanReplay(this, card)) return;
        await PowerCmd.Decrement(this);
    }
}
