using BaseLib.Abstracts;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class DodgePower : PowerModel, ICustomPower
{
    internal const int MaximumAmount = 40;
    internal const int ExpandedMaximumAmount = 80;
    internal const decimal DamageBonusPerStack = 0.025m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => IsMutable
        ? System.Math.Clamp(Amount, 0, DodgeCombatHelper.GetMaximum(Owner)) : 0;

    public override Color AmountLabelColor => new("6ED7FF");
    public string CustomPackedIconPath => ImageHelper.GetImagePath("powers/extreme_dodge_roll_penalty_power.png");
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext,
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Amount <= 0) return;
        int bounded = DodgeCombatHelper.GetAmount(Owner);
        int next = DodgeCombatHelper.RetainsAtTurnStart(Owner) ? bounded : bounded / 2;
        if (next != Amount)
            await PowerCmd.ModifyAmount(choiceContext, this, next - Amount, null, null);
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        dealer == Owner && target != null && target.Side != Owner.Side && amount > 0m &&
        DodgeCombatHelper.GrantsDamageBonus(Owner)
            ? 1m + GetAmountBonus(DodgeCombatHelper.GetAmount(Owner)) : 1m;

    internal static decimal GetAmountBonus(int amount) =>
        System.Math.Clamp(amount, 0, ExpandedMaximumAmount) * DamageBonusPerStack;

    internal void RefreshMaximum()
    {
        if (!IsMutable || Owner == null) return;
        int bounded = DodgeCombatHelper.GetAmount(Owner);
        if (Amount != bounded) SetAmount(bounded);
    }
}
