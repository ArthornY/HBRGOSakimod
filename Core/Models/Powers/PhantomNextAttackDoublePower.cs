using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PhantomNextAttackDoublePower : PowerModel
{
    private const int MaximumSegments = 1000;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int UpgradedApplications { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F2C15F");

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        if (cardSource is MadlyInfatuatedHbrgoSaki
            {
                IsUpgraded: true
            })
        {
            AssertMutable();
            UpgradedApplications++;
        }

        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature applier,
        CardModel cardSource)
    {
        return ReferenceEquals(power, this)
            ? CombatPreviewRefreshHelper.RefreshCreatureIntent(Owner)
            : Task.CompletedTask;
    }

    internal IReadOnlyList<decimal> TransformSegments(
        IReadOnlyList<decimal> source)
    {
        List<decimal> segments = source?.Take(MaximumSegments).ToList() ??
            new List<decimal>();
        if (segments.Count == 0)
        {
            return segments;
        }

        int upgraded = Math.Clamp(
            UpgradedApplications,
            0,
            Math.Max(0, Amount));
        int normal = Math.Max(0, Amount - upgraded);
        for (int index = 0; index < upgraded; index++)
        {
            if (segments.Count < MaximumSegments)
            {
                segments.Add(segments[^1]);
            }

            DuplicateSegments(segments);
        }

        for (int index = 0; index < normal; index++)
        {
            DuplicateSegments(segments);
        }

        return segments;
    }

    private static void DuplicateSegments(List<decimal> segments)
    {
        int count = Math.Min(
            segments.Count,
            MaximumSegments - segments.Count);
        if (count <= 0)
        {
            return;
        }

        segments.AddRange(segments.Take(count).ToArray());
    }
}
