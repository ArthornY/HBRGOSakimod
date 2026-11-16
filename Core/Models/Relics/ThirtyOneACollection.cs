using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using System.Collections.Generic;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class ThirtyOneACollection : TeamCollectionRelic
{
    private bool LegacyDodge => !IsMelted && !HbrBattleStatsHelper.IsEnabled(Owner);
    internal bool HasBlockConversion => LegacyDodge && DisplayAmount >= 2;
    internal bool CountsDodgeAsBlock => LegacyDodge && DisplayAmount >= 4;
    internal bool KeepsDodgeAtTurnStart => LegacyDodge && DisplayAmount >= 4;
    internal bool GrantsDodgeStats => LegacyDodge;
    internal bool HasExpandedDodge => LegacyDodge && DisplayAmount >= 6;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool PendingLive { get; private set; }

    internal override void RefreshCounter()
    {
        base.RefreshCounter();
        if (IsMutable) Owner?.Creature?.GetPower<DodgePower>()?.RefreshMaximum();
    }

    public override string PackedIconPath => ImageHelper.GetImagePath("relics/thirty_one_a.png");
    protected override int CountCards(Player player) => ThirtyOneAKeywordRules.GetPermanentDistinctCount(player);
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        (!IsMutable || HbrBattleStatsHelper.IsEnabled(Owner))
            ? ThirtyOneACutinCombatHelper.HoverTips() : new[] { DodgePower.RelicTip(false) };

    public override async Task BeforeCombatStart()
    {
        if (!PendingLive || IsMelted || !HbrBattleStatsHelper.IsEnabled(Owner) ||
            Owner?.Creature is not { IsAlive: true, CombatState: not null } creature) return;
        PendingLive = false;
        Flash();
        await PowerCmd.Apply<HbrMoralePower>(new BlockingPlayerChoiceContext(), creature, 2m, creature, null);
        ThirtyOneACutinCombatHelper.Show(Owner, "Karen", live: true);
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (!IsMelted && HbrBattleStatsHelper.IsEnabled(Owner) && DisplayAmount >= 6) PendingLive = true;
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player) =>
        player == Owner && !IsMelted && HbrBattleStatsHelper.IsEnabled(Owner)
            ? ThirtyOneACutinCombatHelper.TurnStart(context, this) : Task.CompletedTask;
}
