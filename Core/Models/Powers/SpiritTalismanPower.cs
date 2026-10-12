using System;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SpiritTalismanPower : PowerModel, ICustomPower
{
    internal const int MaximumAmount = 10;
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("B3E5F8");
    public string CustomPackedIconPath => ImageHelper.GetImagePath("powers/extreme_dodge_roll_penalty_power.png");
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal decimal DamageMultiplier => 1m + Math.Clamp(Amount, 0, MaximumAmount) * 0.1m;

    public override Task AfterDamageReceived(PlayerChoiceContext context,
        Creature target, DamageResult result, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (target != Owner || result?.Receiver != Owner || result.UnblockedDamage <= 0 ||
            Owner.IsDead || Amount <= 0 || Amount >= MaximumAmount)
            return Task.CompletedTask;
        return PowerCmd.ModifyAmount(context, this, 1m, dealer, cardSource);
    }
}
