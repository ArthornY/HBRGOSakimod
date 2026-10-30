using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

// Amount is stat reduction. Duration is independent and never presented as
// additional reduction stacks, including a refresh by a weaker application.
[CustomID("HBR_CALAMITY_POWER")]
public sealed class HbrCalamityPower : PowerModel, ICustomPower
{
    internal const int InitialDuration = 2;

    [SavedProperty]
    public int RemainingTurns { get; private set; } = InitialDuration;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.None;
    public string CustomPackedIconPath => "res://images/powers/hbr_calamity_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("Amount", IsMutable ? Math.Max(0, Amount) : 0);
            text.Add("RemainingTurns", Math.Max(0, RemainingTurns));
            text.Add("Turns", Math.Max(0, RemainingTurns));
            return text;
        }
    }

    internal void RefreshDuration()
    {
        AssertMutable();
        RemainingTurns = InitialDuration;
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext,
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner == null || side != Owner.Side || !participants.Contains(Owner) ||
            !ReferenceEquals(Owner.CombatState, combatState)) return;
        // This is an actual turn-start countdown, not native Amount duration.
        // Neither a player-side first-tick skip nor an unrelated extra turn
        // changes it. Native removal releases the reduction when time is up.
        RemainingTurns = Math.Max(0, RemainingTurns - 1);
        Flash();
        if (RemainingTurns == 0 || Amount <= 0)
            await PowerCmd.Remove(this);
        else Owner.InvokePowerModified(this, 0, true);
    }
}
