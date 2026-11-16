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
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class EatPassersbyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F8D36A");

    // Under HBR the stacks are its holder's skill, six to 100%, like Thorns.
    public override LocString Description
    {
        get
        {
            bool owned = IsMutable && Owner != null;
            if (owned ? !HbrBattleStatsHelper.IsEnabled(Owner) : !HbrVigorSkillDescriptionPatch.IsHbrCardHover)
                return base.Description;
            LocString text = new("powers", "EAT_PASSERSBY_POWER.hbrDescription");
            text.Add("DamageSkill", owned ? HbrNativeEffectSkillHelper.FormatPercent(Amount)
                : $"（层数×{HbrNativeEffectSkillHelper.FormatPercent(1m)}）");
            return text;
        }
    }

    public override async Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel cardSource)
    {
        if (creature != Owner ||
            amount <= 0m ||
            Owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Creature[] targets = Owner.CombatState
            .GetOpponentsOf(Owner)
            .Where(creature => creature.IsAlive)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        Flash();
        // Declared as its holder's skill: one coefficient on the holder's stats for every
        // target. Without HBR numerics the declaration stays inactive and deals the stacks.
        using var skill = HbrEffectSkillDamageContext.Begin(null, Owner, null, Amount,
            ValueProp.Unpowered, Owner);
        await CreatureCommandCompatibility.Damage(
            new ThrowingPlayerChoiceContext(),
            targets,
            Amount,
            ValueProp.Unpowered,
            Owner,
            null);
    }
}
