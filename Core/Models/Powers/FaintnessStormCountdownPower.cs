using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class FaintnessStormCountdownPower : PowerModel, ICustomPower
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool SkipTurnEnd { get; private set; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public string CustomPackedIconPath => "res://images/powers/faintness_storm_countdown_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    internal bool IsDue => Owner?.Monster is BigotryGate && EnemyStatScalingHelper.IsEnemy(Owner) &&
        HbrBattleStatsHelper.IsEnabled(Owner) && Amount <= 1;
    public override LocString Description
    {
        get
        {
            LocString text = new("powers", Id.Entry + (!IsMutable || Owner == null ? ".referenceDescription" : ".description"));
            text.Add("Remaining", IsMutable ? System.Math.Clamp(Amount, 1, 4) : 4);
            return text;
        }
    }
    internal void ResetAfterStorm()
    {
        AssertMutable(); SetAmount(4); SkipTurnEnd = true; InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
    }
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side,
        IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner?.Monster is not BigotryGate || !EnemyStatScalingHelper.IsEnemy(Owner) ||
            !HbrBattleStatsHelper.IsEnabled(Owner) || side != CombatSide.Enemy) return Task.CompletedTask;
        if (SkipTurnEnd) SkipTurnEnd = false;
        else SetAmount(Amount > 1 ? Amount - 1 : 4);
        InvokeDisplayAmountChanged(); Owner.InvokePowerModified(this, 0, true);
        HbrEnemyFlavorSchedulingHelper.RefreshOwner(Owner);
        return Task.CompletedTask;
    }
}
