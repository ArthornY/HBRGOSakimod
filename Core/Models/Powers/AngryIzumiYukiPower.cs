using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class AngryIzumiYukiPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F85C5C");

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        DodgePower dodge = Owner.GetPower<DodgePower>();
        if (dodge == null || dodge.Amount <= 0 || Amount <= 0)
        {
            return;
        }

        Flash();
        await PowerCmd.ModifyAmount(choiceContext, dodge, -System.Math.Min(dodge.Amount, Amount), Owner, null);
    }
}
