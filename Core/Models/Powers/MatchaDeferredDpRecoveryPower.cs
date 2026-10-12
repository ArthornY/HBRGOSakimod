using System;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class MatchaDeferredDpRecoveryPower : PowerModel, ICustomPower
{
    // Every play is a separate 10% recovery. Preserve source order and each
    // card's overflow upgrade instead of merging differently upgraded cards.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] PendingOverflowConversions { get; private set; } = Array.Empty<int>();

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("6ED7FF");
    public string CustomPackedIconPath => ModelDb.Power<RegenPower>().IconPath;
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            bool usesThirtyAEffects = IsMutable && Owner?.Player != null
                ? Owner.Player.Character is HbrgoSaki30A
                : HbrgoSakiCardPortraitContext.LibraryVariant == HbrgoSakiVariant.ThirtyA;
            return usesThirtyAEffects
                ? new LocString("powers", Id.Entry + ".description.30a")
                : base.Description;
        }
    }

    internal static async Task Queue(PlayerChoiceContext context, Player player,
        bool convertOverflow, CardModel source)
    {
        MatchaDeferredDpRecoveryPower power = await PowerCmd.Apply<MatchaDeferredDpRecoveryPower>(
            context, player.Creature, 1m, player.Creature, source);
        if (power != null)
            power.PendingOverflowConversions = power.PendingOverflowConversions
                .Append(convertOverflow ? 1 : 0).ToArray();
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || AmountOnTurnStart <= 0) return;

        int count = Math.Min(AmountOnTurnStart, PendingOverflowConversions.Length);
        if (count <= 0) return;

        int[] pending = PendingOverflowConversions.Take(count).ToArray();
        PendingOverflowConversions = PendingOverflowConversions.Skip(count).ToArray();
        // The native turn-start snapshot excludes new reservations made by
        // auto-play hooks during this very turn's setup.
        AmountOnTurnStart = 0;
        Flash();
        await PowerCmd.ModifyAmount(choiceContext, this, -count, null, null);
        foreach (int convertOverflow in pending)
            await HbrMatchaDpHelper.Restore(choiceContext, player, 0.10m, convertOverflow != 0);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        PendingOverflowConversions = (int[])PendingOverflowConversions.Clone();
    }
}
