using System;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruSeeYouAgainHelper
{
    internal static decimal Pow2(int layers)
    {
        if (layers <= 0) return 1m;
        if (layers >= 96) return decimal.MaxValue;
        decimal result = 1m;
        for (int index = 0; index < layers; index++) result *= 2m;
        return result;
    }

    internal static decimal GetMultiplier(Player player) =>
        player?.Creature?.IsAlive == true && !CombatManager.Instance.IsOverOrEnding
            ? Pow2(player.Creature.GetPower<HotaruSeeYouAgainPower>()?.ActiveCopies ?? 0) : 1m;

    internal static decimal GetCannonMultiplier(Creature cannon) =>
        cannon?.Monster is HotaruCannonModel { Kind: HotaruCannonKind.Longing } &&
        Millsage.IsOwnedCannon(cannon, cannon.PetOwner)
            ? GetMultiplier(cannon.PetOwner) : 1m;

    internal static decimal GetExtraDexterity(Player player) =>
        player?.GetRelic<Millsage>()?.HasCannonUpgrade(HotaruCannonKind.Longing, 2) == true
            ? Math.Min(999_999_999m, DamageAmplificationLimits.MultiplySaturated(2m, GetMultiplier(player) - 1m)) : 0m;

    internal static Task OnTurnStart(PlayerChoiceContext context, Player player, object token) =>
        player?.Creature?.GetPower<HotaruSeeYouAgainPower>()?.OnTurnStart(context, token) ?? Task.CompletedTask;

    internal static Task OnTurnEnd(PlayerChoiceContext context, Player player, object token) =>
        player?.Creature?.GetPower<HotaruSeeYouAgainPower>()?.OnTurnEnd(context, token) ?? Task.CompletedTask;
}
