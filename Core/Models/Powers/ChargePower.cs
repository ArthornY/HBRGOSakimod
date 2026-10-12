using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ChargePower : PowerModel
{
    public const int RangeShiftPerStack = 2;
    public const int AbsoluteCriticalReductionPerStack = 20;

    private const int _evilEyeTidalSlashThreshold = 3;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("FFD45C");

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("criticalThreshold", RollKeywordRules.GetCriticalMinimumForCharge(IsMutable ? Amount : 0));
            text.Add("absoluteThreshold", RollKeywordRules.GetAbsoluteCriticalMinimumForCharge(IsMutable ? Amount : 0));
            return text;
        }
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (Amount <= _evilEyeTidalSlashThreshold ||
            card is not MimicEvilEyeTidalSlashHbrgoSaki ||
            card.Owner?.Creature != Owner ||
            originalCost <= 0m)
        {
            return false;
        }

        modifiedCost = originalCost * 0.5m;
        return true;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Amount <= 0)
        {
            return;
        }

        Flash();
        await PowerCmd.TickDownDuration(this);
    }
}
