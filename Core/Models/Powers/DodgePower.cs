using BaseLib.Abstracts;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class DodgePower : PowerModel, ICustomPower
{
    internal const int MaximumAmount = 40;
    internal const int ExpandedMaximumAmount = 80;
    // 31A's Strength and Dexterity from layers; see DodgeCombatHelper.GetStatBonus.
    internal const int HbrStatsPerLayer = 2;
    internal const int NativeLayersPerStat = 5;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => IsMutable
        ? System.Math.Clamp(Amount, 0, DodgeCombatHelper.GetMaximum(Owner)) : 0;

    public override Color AmountLabelColor => new("6ED7FF");
    public string CustomPackedIconPath => ImageHelper.GetImagePath("powers/extreme_dodge_roll_penalty_power.png");
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    [System.ThreadStatic] private static bool? _relicTipHbr;

    // Under HBR each layer stands for 100 points. A live power follows its owner and, with the
    // 31A relic, states the Strength and Dexterity it grants now; the canonical tip follows
    // the 31A relic or card whose hover tips are being built.
    public override LocString Description
    {
        get
        {
            bool owned = IsMutable && Owner != null;
            bool hbr = owned ? HbrBattleStatsHelper.IsEnabled(Owner)
                : _relicTipHbr ?? HbrVigorSkillDescriptionPatch.IsHbrCardHover;
            bool live = owned && DodgeCombatHelper.GrantsStats(Owner);
            if (!hbr && !live) return base.Description;
            LocString text = new("powers", Id.Entry + (hbr
                ? live ? ".hbrOwnedDescription" : ".hbrDescription"
                : ".ownedDescription"));
            if (live) text.Add("DodgeStats", DodgeCombatHelper.GetStatBonus(Owner));
            return text;
        }
    }

    internal static IHoverTip RelicTip(bool hbr)
    {
        bool? previous = _relicTipHbr;
        _relicTipHbr = hbr;
        try { return HoverTipFactory.FromPower<DodgePower>(); }
        finally { _relicTipHbr = previous; }
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext,
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Amount <= 0) return;
        int bounded = DodgeCombatHelper.GetAmount(Owner);
        int next = DodgeCombatHelper.RetainsAtTurnStart(Owner) ? bounded : bounded / 2;
        if (next != Amount)
            await PowerCmd.ModifyAmount(choiceContext, this, next - Amount, null, null);
    }

    // Native Strength and Dexterity from the layers, with native Strength's and Dexterity's
    // eligibility. HBR reads the same amount as attributes in HbrStatPowerHelper instead,
    // and never as a flat bonus.
    public decimal ModifyDamageAdditive(Creature target, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource) =>
        dealer == Owner && props.IsPoweredAttack() && !HbrBattleStatsHelper.IsEnabled(Owner)
            ? DodgeCombatHelper.GetStatBonus(Owner) : 0m;

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props,
        CardModel cardSource, CardPlay cardPlay)
    {
        if (HbrBattleStatsHelper.IsEnabled(Owner)) return 0m;
        if (cardSource != null ? cardSource.Owner?.Creature != Owner : target != Owner) return 0m;
        return props.IsPoweredCardOrMonsterMoveBlock() ? DodgeCombatHelper.GetStatBonus(Owner) : 0m;
    }

    internal void RefreshMaximum()
    {
        if (!IsMutable || Owner == null) return;
        int bounded = DodgeCombatHelper.GetAmount(Owner);
        if (Amount != bounded) SetAmount(bounded);
    }
}
