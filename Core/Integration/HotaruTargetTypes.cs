using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Combat.CardTargeting;

namespace HBRGOSakiMod.Core.Integration;

public static class HotaruTargetTypes
{
    public static TargetType MaxLevelOwnedCannon { get; } = CustomTargetType.RegisterSingleTargetType(
        "HBRGOSakiMod.Hotaru.MaxLevelOwnedCannon", "满级己方浮游炮",
        (creature, player) => player?.GetRelic<Millsage>()?.CanRetireCannon(creature) == true);
    public static TargetType OwnedCannon { get; } = CustomTargetType.RegisterSingleTargetType(
        "HBRGOSakiMod.Hotaru.OwnedCannon", "己方浮游炮", Millsage.IsOwnedCannon);
    public static TargetType OwnedCannonOrEnemy { get; } = CustomTargetType.RegisterSingleTargetType(
        "HBRGOSakiMod.Hotaru.OwnedCannonOrEnemy", "己方浮游炮或敌人",
        (creature, player) => Millsage.IsOwnedCannon(creature, player) || Millsage.IsEnemy(creature, player));
}
