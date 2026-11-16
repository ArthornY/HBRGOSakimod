using System;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// Six senses are level points only (2026-11-09): cards carry no attribute and
/// enemies no resistance. A level still raises its HBR battle stat and counts
/// toward the memory garden; this reads the effective level for other systems,
/// such as events gated on a sense.
/// </summary>
internal static class SixSenseCombatHelper
{
    internal static decimal Level(Player player, SixSenseAttribute attribute) =>
        Math.Max(0m, player?.GetRelic<ElectronicSoldierManual>()?.GetEffectiveSenseAmount(attribute) ?? 0m);
}
