using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruCannonUpgrades
{
    internal static bool CanAccess(Player player) => player?.Character is HbrgoHotaru &&
        player.GetRelic<ElectronicSoldierManual>()?.IsMutable == true && player.GetRelic<Millsage>()?.IsMutable == true;

    internal static bool HasAvailableUpgrade(Player player) => CanAccess(player) &&
        HotaruCannonKinds.Original.Any(kind => player.GetRelic<Millsage>().IsCannonUnlocked(kind) &&
            player.GetRelic<Millsage>().GetCannonUpgradeLevel(kind) < Millsage.MaximumCannonUpgradeLevel);

    internal static string IconPath(HotaruCannonKind kind) =>
        "res://images/characters/hbrgo_hotaru/cannons/" + kind.ToString().ToLowerInvariant() + ".png";

    private static MonsterModel Canonical(HotaruCannonKind kind) => kind switch
    {
        HotaruCannonKind.Happiness => ModelDb.Monster<HotaruHappinessCannon>(),
        HotaruCannonKind.Coffin => ModelDb.Monster<HotaruCoffinCannon>(),
        HotaruCannonKind.Contest => ModelDb.Monster<HotaruContestCannon>(),
        HotaruCannonKind.Longing => ModelDb.Monster<HotaruLongingCannon>(),
        HotaruCannonKind.Absolution => ModelDb.Monster<HotaruAbsolutionCannon>(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    internal static string CannonTitle(HotaruCannonKind kind) => Canonical(kind).Title.GetFormattedText();

    internal static string BaseText(HotaruCannonKind kind) =>
        new LocString("rest_site_ui", $"HOTARU_CANNON_UPGRADE.{kind.ToString().ToLowerInvariant()}.base").GetFormattedText();

    internal static string UpgradeText(HotaruCannonKind kind, int tier) =>
        new LocString("rest_site_ui", $"HOTARU_CANNON_UPGRADE.{kind.ToString().ToLowerInvariant()}.{tier}").GetFormattedText();

    internal static string ObtainedEffects(Millsage relic, HotaruCannonKind kind) =>
        string.Join("\n", Enumerable.Range(1, relic.GetCannonUpgradeLevel(kind)).Select(tier => "• " +
            HbrManualGlossaryPresentation.FormatDescription(UpgradeText(kind, tier), "hotaru_cannon")));

    internal static string DamageMultiplierText(int level, int currentPercent)
    {
        LocString text = new("rest_site_ui", "HOTARU_CANNON_UPGRADE.damageMultiplier");
        text.Add("current", currentPercent);
        text.Add("maximum", Millsage.InitialCannonDamagePercent + Millsage.CannonDamagePercentPerUpgrade * level);
        return text.GetFormattedText();
    }

    private static string Description(HotaruCannonKind kind, int level, int currentPercent)
    {
        string basic = DamageMultiplierText(level, currentPercent) + "\n\n" + BaseText(kind);
        return level == 0 ? basic : basic + "\n\n" +
            string.Join("\n", Enumerable.Range(1, level).Select(tier => "• " + UpgradeText(kind, tier)));
    }

    internal static IReadOnlyList<HotaruCannonUpgradeChoice> GetChoices(Player player)
    {
        if (!CanAccess(player)) return Array.Empty<HotaruCannonUpgradeChoice>();
        ElectronicSoldierManual manual = player.GetRelic<ElectronicSoldierManual>();
        Millsage relic = player.GetRelic<Millsage>();
        manual.SynchronizeMemoryGardenUpgradePoints();
        return HotaruCannonKinds.Original.Select(kind =>
        {
            int level = relic.GetCannonUpgradeLevel(kind);
            int currentPercent = relic.GetCannonDamagePercent(kind);
            int nextLevel = Math.Min(Millsage.MaximumCannonUpgradeLevel, level + 1);
            int nextPercent = level == nextLevel ? currentPercent : currentPercent + Millsage.CannonDamagePercentPerUpgrade;
            bool unlocked = relic.IsCannonUnlocked(kind);
            bool complete = level >= Millsage.MaximumCannonUpgradeLevel;
            bool available = unlocked && !complete && manual.MemoryGardenUpgradePoints > 0;
            string status = !unlocked ? Text("locked") : complete ? Text("complete")
                : manual.MemoryGardenUpgradePoints <= 0 ? Text("noPoints") : UpgradeText(kind, level + 1);
            return new HotaruCannonUpgradeChoice(kind,
                CannonTitle(kind) + $" · {level}/{Millsage.MaximumCannonUpgradeLevel}", IconPath(kind),
                Description(kind, level, currentPercent), Description(kind, nextLevel, nextPercent), available, status);
        }).ToArray();
    }

    internal static async Task<bool> Upgrade(Player player)
    {
        if (!CanAccess(player) || player.RunState.CurrentRoom is not RestSiteRoom) return false;
        ElectronicSoldierManual manual = player.GetRelic<ElectronicSoldierManual>();
        Millsage relic = player.GetRelic<Millsage>();
        IReadOnlyList<HotaruCannonUpgradeChoice> choices = GetChoices(player);
        if (!choices.Any(choice => choice.IsAvailable)) return false;
        Dictionary<HotaruCannonKind, int> levels = choices.ToDictionary(choice => choice.Kind, choice => relic.GetCannonUpgradeLevel(choice.Kind));
        MemoryGardenSelectionScope scope = MemoryGardenSelectionScope.Capture(player);
        uint choiceId = scope.Synchronizer.ReserveChoiceId(player);
        int? selected;
        if (LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != NetGameType.Replay)
        {
            HotaruCannonKind? kind = TestMode.IsOn ? choices.First(choice => choice.IsAvailable).Kind
                : await NHotaruCannonUpgradeScreen.Choose(choices, manual.MemoryGardenUpgradePoints);
            scope.ThrowIfInvalid();
            selected = kind.HasValue && choices.Any(choice => choice.Kind == kind && choice.IsAvailable) ? (int)kind.Value : null;
            scope.Synchronizer.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndex(selected));
        }
        else selected = (await scope.Synchronizer.WaitForRemoteChoice(player, choiceId)).AsIndexOrNull();
        scope.ThrowIfInvalid();
        if (!selected.HasValue || !Enum.IsDefined((HotaruCannonKind)selected.Value)) return false;
        HotaruCannonKind selectedKind = (HotaruCannonKind)selected.Value;
        if (!choices.Any(choice => choice.Kind == selectedKind && choice.IsAvailable) ||
            !ReferenceEquals(player.GetRelic<ElectronicSoldierManual>(), manual) || !ReferenceEquals(player.GetRelic<Millsage>(), relic)) return false;
        bool purchased = Purchase(player, relic, manual, selectedKind, levels[selectedKind]);
        if (purchased && relic.GetCannonUpgradeLevel(selectedKind) == Millsage.MaximumCannonUpgradeLevel)
            await HotaruEndgameCardChoice.TryOffer(player, selectedKind);
        return purchased;
    }

    internal static bool Purchase(Player player, Millsage relic, ElectronicSoldierManual manual,
        HotaruCannonKind kind, int expectedLevel)
    {
        if (!CanAccess(player) || !MemoryGardenSelectionScope.IsCurrentRun(player) ||
            player.RunState.CurrentRoom is not RestSiteRoom || !ReferenceEquals(player.GetRelic<Millsage>(), relic) ||
            !ReferenceEquals(player.GetRelic<ElectronicSoldierManual>(), manual) || !relic.IsCannonUnlocked(kind) ||
            expectedLevel < 0 || expectedLevel >= Millsage.MaximumCannonUpgradeLevel || relic.GetCannonUpgradeLevel(kind) != expectedLevel ||
            !MemoryGardenUpgradePointsHelper.TryBeginPurchase(manual, out IDisposable purchase)) return false;
        using (purchase)
        {
            if (!IsPurchaseCurrent(player, relic, manual, kind, expectedLevel)) return false;
            MemoryGardenSelectionScope scope = MemoryGardenSelectionScope.Capture(player);
            if (!manual.TrySpendMemoryGardenUpgradePoint()) return false;
            bool attempted = false;
            bool committed = false;
            try
            {
                scope.ThrowIfInvalid();
                if (!IsPurchaseCurrent(player, relic, manual, kind, expectedLevel)) return false;
                attempted = true;
                committed = relic.TryUpgradeCannon(kind, expectedLevel);
                return committed;
            }
            finally
            {
                // Only our attempted mutation can keep this payment after a
                // publication throws. A changed selection before it is refunded.
                committed |= attempted && relic.GetCannonUpgradeLevel(kind) > expectedLevel;
                if (!committed) manual.RefundMemoryGardenUpgradePoint();
                // A capped cannon can become unavailable without another balance
                // change; update both funded buttons after the upgrade commits.
                MemoryGardenUpgradePointsHelper.NotifyUpgradePointsChanged(manual,
                    manual.MemoryGardenUpgradePoints, manual.MemoryGardenUpgradePoints, "cannon-upgrade");
            }
        }
    }

    private static bool IsPurchaseCurrent(Player player, Millsage relic, ElectronicSoldierManual manual,
        HotaruCannonKind kind, int expectedLevel) => CanAccess(player) &&
        MemoryGardenSelectionScope.IsCurrentRun(player) && player.RunState.CurrentRoom is RestSiteRoom &&
        ReferenceEquals(player.GetRelic<Millsage>(), relic) && ReferenceEquals(player.GetRelic<ElectronicSoldierManual>(), manual) &&
        relic.IsCannonUnlocked(kind) && relic.GetCannonUpgradeLevel(kind) == expectedLevel;

    private static string Text(string key) => new LocString("rest_site_ui", "HOTARU_CANNON_UPGRADE." + key).GetFormattedText();
}
