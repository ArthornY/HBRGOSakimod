using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib;

namespace HBRGOSakiMod.Core.Integration;

internal static class HBRGOSakiTargetTypes
{
    private const string ModId = "HBRGOSakiMod";

    private static bool _registered;
    private static TargetType _friendlyPlayerIncludingSelf;

    internal static TargetType FriendlyPlayerIncludingSelf
    {
        get
        {
            Register();
            return _friendlyPlayerIncludingSelf;
        }
    }

    internal static void Register()
    {
        if (_registered)
        {
            return;
        }

        _friendlyPlayerIncludingSelf = RitsuLibFramework.RegisterSingleTargetType(
            ModId,
            "FriendlyPlayerIncludingSelf",
            CanTargetFriendlyPlayerIncludingSelf);
        _registered = true;
    }

    private static bool CanTargetFriendlyPlayerIncludingSelf(
        Creature target,
        Player player)
    {
        return target.IsAlive &&
            target.IsHittable &&
            target.Player != null &&
            target.Side == player.Creature.Side;
    }
}
