using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class ThirtyOneBCollection : TeamCollectionRelic
{
    private int _lastStatCount = -1;
    public override string PackedIconPath => ImageHelper.GetImagePath("relics/thirty_a.png");
    protected override int CountCards(Player player) => ThirtyOneBKeywordRules.GetPermanentDistinctCount(player);
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<ProtectiveCharmPower>() };

    internal override void RefreshCounter()
    {
        base.RefreshCounter();
        int count = DisplayAmount;
        if (count == _lastStatCount) return;
        _lastStatCount = count;
        if (Owner?.Creature?.CombatState != null)
            CombatPreviewRefreshHelper.RefreshCombatState(Owner.Creature, nameof(ThirtyOneBCollection));
    }

    public override async Task BeforeCombatStart()
    {
        if (IsMelted || DisplayAmount < 2 || !HbrBattleStatsHelper.IsEnabled(Owner) ||
            Owner?.Creature is not { IsAlive: true, CombatState: not null } creature) return;
        Flash();
        await ThirtyOneBBondHelper.GrantCharm(new BlockingPlayerChoiceContext(), creature, DisplayAmount, creature);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player != Owner || IsMelted || DisplayAmount < 6 || !HbrBattleStatsHelper.IsEnabled(Owner) ||
            Owner?.Creature is not { IsAlive: true, CombatState: not null } creature ||
            creature.GetPowerAmount<ProtectiveCharmPower>() > 0) return;
        Flash();
        await ThirtyOneBBondHelper.GrantCharm(context, creature, 1m, creature);
    }

    internal void AddDescriptionVariables(LocString text)
    {
        int count = IsMutable && Owner != null ? DisplayAmount : 0;
        text.Add("Kinds", count);
        text.Add("ActiveTiers", count >= 6 ? "护身符、互相守护、友情不灭" : count >= 4 ? "护身符、互相守护" : count >= 2 ? "护身符" : "无");
        text.Add("NextTier", count >= 6 ? "全部档位已生效" : count >= 4 ? "6种：每回合开始补回用尽的护身符" : count >= 2 ? "4种：每层护身符使体力、精神各+4%" : "2种：开局获得等同种类数的护身符");
    }
}
