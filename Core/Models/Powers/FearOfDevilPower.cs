using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
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

public sealed class FearOfDevilPower : PowerModel, ICustomPower
{
    internal const int ReductionPerStack = 30;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool Permanent { get; private set; }
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    // Each incoming application crosses the real native receiving/Artifact
    // hooks once. Accepted applications merge into a single visible counter.
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public string CustomPackedIconPath => "res://images/ui/thirty_one_c/fear_of_devil_icon.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("Amount", IsMutable ? Math.Max(0, Amount) : 1);
            text.Add("Reduction", (decimal)(IsMutable ? Math.Max(0, Amount) : 1) * ReductionPerStack);
            text.Add("Duration", Permanent ? "战斗结束" : "敌方回合结束");
            return text;
        }
    }
    internal static decimal GetTotalReduction(Creature target)
    {
        if (target == null) return 0m;
        // Native Apply can yield before AfterApplied merges the accepted
        // application. Max keeps that transient overlap from doubling E.
        int permanent = 0; bool temporary = false;
        foreach (PowerModel model in target.Powers)
            if (model is FearOfDevilPower { Amount: > 0 } fear)
            {
                if (fear.Permanent) permanent = Math.Max(permanent, fear.Amount);
                else temporary = true;
            }
        return (decimal)(permanent > 0 ? permanent : temporary ? 1 : 0) * ReductionPerStack;
    }
    internal static async Task Apply(PlayerChoiceContext context, Creature target, Creature applier, bool permanent)
    {
        if (target?.IsAlive != true || !EnemyStatScalingHelper.IsEnemy(target) || !HbrBattleStatsHelper.IsEnabled(target)) return;
        var power = (FearOfDevilPower)ModelDb.Power<FearOfDevilPower>().ToMutable();
        power.Permanent = permanent;
        await PowerCmd.Apply(context, power, target, 1m, applier, null);
    }
    public override async Task AfterApplied(Creature applier, CardModel source)
    {
        if (Owner?.Powers.Contains(this) != true || Amount <= 0) return;
        FearOfDevilPower[] others = Owner.Powers.OfType<FearOfDevilPower>()
            .Where(power => !ReferenceEquals(power, this) && power.Amount > 0).ToArray();
        if (!Permanent && others.Any(power => power.Permanent))
        { await PowerCmd.Remove(this); return; }
        decimal previousPermanent = others.Where(power => power.Permanent).Aggregate(0m,
            (sum, power) => DamageAmplificationLimits.AddSaturated(sum, power.Amount));
        int stacks = Permanent ? (int)Math.Min(int.MaxValue, 1m + previousPermanent +
            (previousPermanent == 0m && others.Length > 0 ? 1m : 0m)) : 1;
        SetAmount(stacks, silent: true);
        // Calling Remove starts every native removal synchronously before its
        // visual await, so a later accepted application sees one merged ledger.
        Task[] removals = others.Select(power => PowerCmd.Remove(power)).ToArray();
        await Task.WhenAll(removals);
        await CombatPreviewRefreshHelper.RefreshCreatureIntent(Owner);
    }
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants) =>
        !Permanent && side == CombatSide.Enemy && !ExtraTurnDurationHelper.ShouldSuppressDurationTick(this)
            ? PowerCmd.Remove(this) : Task.CompletedTask;
    public override Task AfterRemoved(Creature oldOwner) => CombatPreviewRefreshHelper.RefreshCreatureIntent(oldOwner);
}
