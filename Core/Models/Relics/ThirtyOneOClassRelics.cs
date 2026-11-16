using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public abstract class ThirtyOneOClassRelic : RelicModel, IThirtyOneOClassRelic
{
    // Existing art is deliberately a placeholder until the supplied icons arrive.
    public override string PackedIconPath => "res://images/relics/soul_of_anchor.png";
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => PackedIconPath;
    protected bool CanTrigger => IsMutable && !IsMelted && ThirtyOneOClassRelicHelper.IsEnabled(Owner);
    protected static HoverTip Term(string key) => new(new LocString("static_hover_tips", key + ".title"),
        new LocString("static_hover_tips", key + ".description"));
}

public sealed class MetronomeThirtyOneO : ThirtyOneOClassRelic
{
    private sealed class TurnUse { internal int Sides; }
    private ConditionalWeakTable<object, TurnUse> _extraTurns = new();
    [SavedProperty] public int LastOrdinaryTurn { get; private set; } = -1;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public int OrdinaryTurnSides { get; private set; }
    public override RelicRarity Rarity => RelicRarity.Common;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { Term("HBR_CLASS_RELIC_MODE_SWITCH") };
    public override Task BeforeCombatStart()
    {
        if (IsMutable) { LastOrdinaryTurn = -1; OrdinaryTurnSides = 0; _extraTurns = new(); }
        return Task.CompletedTask;
    }
    internal async Task RecordModeSwitch()
    {
        if (!CanTrigger || Owner.PlayerCombatState is not { TurnNumber: > 0 } state ||
            Owner.Creature.CombatState is not { } combat || !Owner.Creature.IsAlive) return;
        int side = combat.CurrentSide == CombatSide.Enemy ? 2 : 1;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner);
        if (token == null)
        {
            if (LastOrdinaryTurn != state.TurnNumber) { LastOrdinaryTurn = state.TurnNumber; OrdinaryTurnSides = 0; }
            if ((OrdinaryTurnSides & side) != 0) return;
            OrdinaryTurnSides |= side;
        }
        else
        {
            TurnUse use = _extraTurns.GetOrCreateValue(token);
            if ((use.Sides & side) != 0) return;
            use.Sides |= side;
        }
        Flash();
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1m, Owner);
    }
    protected override void AfterCloned() { base.AfterCloned(); _extraTurns = new(); }
}

public sealed class MarkOfSeven : ThirtyOneOClassRelic
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public int PendingCharge { get; private set; }
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<ChargePower>(), HoverTipFactory.FromPower<SwordheartPower>() };
    public override Task BeforeCombatStart() { if (IsMutable) PendingCharge = 0; return Task.CompletedTask; }
    internal void QueueCharge()
    {
        if (CanTrigger) PendingCharge = (int)Math.Min(BigCombatNumbers.NativeStatCap, (long)PendingCharge + 1L);
    }
    internal async Task ResolvePending(PlayerChoiceContext context)
    {
        if (!CanTrigger || PendingCharge <= 0) return;
        int amount = PendingCharge; PendingCharge = 0;
        Flash();
        await PowerCmd.Apply<ChargePower>(context, Owner.Creature, amount, Owner.Creature, null);
    }
}

public sealed class BoneChinaTeacup : ThirtyOneOClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { Term("HBR_CLASS_RELIC_SIX_SENSE_POINTS") };
    public override Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (CanTrigger && player == Owner && !isMimicked && Owner.GetRelic<ElectronicSoldierManual>() is { } manual)
        { Flash(); manual.GrantSixSensePoints(1); }
        return Task.CompletedTask;
    }
}

public sealed class SakuraSwordTassel : ThirtyOneOClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<SwordheartPower>(), HoverTipFactory.FromCard<MagicSwordLuckySevenHbrgoSaki>() };
}

public sealed class ScoreOfHaruhikage : ThirtyOneOClassRelic
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public bool TriggeredThisCombat { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public bool RewardPending { get; private set; }
    public override RelicRarity Rarity => RelicRarity.Rare;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { Term("HBR_CLASS_RELIC_CRITICAL_SUCCESS") };
    public override Task BeforeCombatStart()
    { if (IsMutable) { TriggeredThisCombat = false; RewardPending = false; } return Task.CompletedTask; }
    internal void QueueCriticalReward()
    {
        if (!CanTrigger || TriggeredThisCombat) return;
        TriggeredThisCombat = true; RewardPending = true;
    }
    internal async Task ResolvePending(PlayerChoiceContext context)
    {
        if (!CanTrigger || !RewardPending) return;
        RewardPending = false;
        Flash();
        await PlayerCmd.GainEnergy(1m, Owner);
        await CardPileCmd.Draw(context, 1m, Owner);
    }
}

public sealed class MusicStand : ThirtyOneOClassRelic
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public bool OfferedThisCombat { get; private set; }
    public override RelicRarity Rarity => RelicRarity.Shop;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { Term("HBR_CLASS_RELIC_FORM_CARD") };
    public override Task BeforeCombatStart() { if (IsMutable) OfferedThisCombat = false; return Task.CompletedTask; }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (!CanTrigger || player != Owner || OfferedThisCombat || Owner.PlayerCombatState?.TurnNumber != 1 ||
            OverclockImmediateTurnCoordinator.GetTurnToken(Owner) != null) return;
        OfferedThisCombat = true;
        CardPile draw = Owner.PlayerCombatState.DrawPile;
        if (!draw.Cards.Any(ThirtyOneOClassRelicHelper.IsFormCard)) return;
        CardModel selected = (await CardSelectCmd.FromCombatPile(context, draw, Owner,
            new CardSelectorPrefs(new LocString("relics", Id.Entry + ".selectionPrompt"), 1),
            ThirtyOneOClassRelicHelper.IsFormCard)).FirstOrDefault();
        if (selected == null || selected.Owner != Owner || selected.Pile != draw ||
            !ThirtyOneOClassRelicHelper.IsFormCard(selected)) return;
        Flash();
        await CardPileCmd.Add(selected, PileType.Hand);
    }
}
