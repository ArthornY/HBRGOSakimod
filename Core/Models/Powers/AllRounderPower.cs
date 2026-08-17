using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class AllRounderPower : PowerModel
{
    private const decimal _baseSenseMultiplier = 1.5m;
    private const decimal _upgradedSenseMultiplier = 2m;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IsEnhanced { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LeadershipBonusGranted { get; private set; }

    public decimal SenseMultiplier => IsEnhanced
        ? _upgradedSenseMultiplier
        : _baseSenseMultiplier;

    public override LocString Description => IsEnhanced
        ? new LocString("powers", "ALL_ROUNDER_POWER.upgradedDescription")
        : base.Description;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        if (cardSource is AllRounderHbrgoSaki { HasEnhancedMultiplier: true })
        {
            SetEnhanced();
        }

        ElectronicSoldierManual manual = Owner.Player?.GetRelic<ElectronicSoldierManual>();
        if (manual == null || Owner.CombatState == null)
        {
            return;
        }

        int baseStrength = manual.LeadershipCharm / 2;
        int boostedStrength = (int)(
            manual.LeadershipCharm * SenseMultiplier / 2m);
        int totalBonus = Math.Max(0, boostedStrength - baseStrength);
        int amountToGrant = Math.Max(0, totalBonus - LeadershipBonusGranted);
        SetLeadershipBonusGranted(totalBonus);
        if (amountToGrant <= 0)
        {
            return;
        }

        Creature[] allies = Owner.CombatState.Creatures
            .Where(creature => creature.Side == Owner.Side && creature.IsAlive)
            .ToArray();
        if (allies.Length == 0)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            allies,
            amountToGrant,
            Owner,
            cardSource);
    }

    private void SetEnhanced()
    {
        AssertMutable();
        IsEnhanced = true;
    }

    private void SetLeadershipBonusGranted(int amount)
    {
        AssertMutable();
        LeadershipBonusGranted = Math.Max(0, amount);
    }
}
