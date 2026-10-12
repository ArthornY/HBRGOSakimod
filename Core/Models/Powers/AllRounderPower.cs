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

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        if (cardSource is AllRounderHbrgoSaki { HasEnhancedMultiplier: true })
        {
            SetEnhanced();
        }

        return Task.CompletedTask;
    }

    private void SetEnhanced()
    {
        AssertMutable();
        IsEnhanced = true;
    }

}
