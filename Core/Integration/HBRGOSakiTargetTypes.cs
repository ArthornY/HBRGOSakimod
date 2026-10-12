using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using HBRGOSakiMod.Core.Models.Powers;
using STS2RitsuLib;

namespace HBRGOSakiMod.Core.Integration;

internal static class HBRGOSakiTargetTypes
{
    private const string ModId = "HBRGOSakiMod";

    private static bool _registered;
    private static TargetType _friendlyPlayerOrPhantom;
    private static TargetType _ownedPhantomOrEnemy;

    internal static TargetType FriendlyPlayerOrPhantom
    {
        get
        {
            Register();
            return _friendlyPlayerOrPhantom;
        }
    }

    internal static TargetType OwnedPhantomOrEnemy
    {
        get
        {
            Register();
            return _ownedPhantomOrEnemy;
        }
    }

    internal static void Register()
    {
        if (_registered)
        {
            return;
        }

        _friendlyPlayerOrPhantom = RitsuLibFramework.RegisterSingleTargetType(
            ModId,
            // Preserve the registered ID used by existing song cards.
            "FriendlyPlayerIncludingSelf",
            CanTargetFriendlyPlayerOrPhantom);
        _ownedPhantomOrEnemy = RitsuLibFramework.RegisterSingleTargetType(
            ModId,
            "OwnedPhantomOrEnemy",
            CanTargetOwnedPhantomOrEnemy);
        _registered = true;
    }

    private static bool CanTargetFriendlyPlayerOrPhantom(
        Creature target,
        Player player)
    {
        return target != null &&
            player?.Creature != null &&
            target.IsAlive &&
            target.IsHittable &&
            target.Side == player.Creature.Side &&
            (target.Player != null ||
                (target.PetOwner != null && target.HasPower<PhantomPower>()));
    }

    private static bool CanTargetOwnedPhantomOrEnemy(
        Creature target,
        Player player)
    {
        if (target == null ||
            player?.Creature == null ||
            !target.IsAlive ||
            !target.IsHittable)
        {
            return false;
        }

        if (target.Side != player.Creature.Side)
        {
            return true;
        }

        return target.PetOwner == player &&
            target.HasPower<PhantomPower>();
    }
}
