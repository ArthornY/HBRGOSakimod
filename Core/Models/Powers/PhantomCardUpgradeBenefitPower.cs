using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PhantomCardUpgradeBenefitPower : PowerModel, ICustomPower
{
    public string CustomPackedIconPath => ModelDb.Power<StrengthPower>().IconPath;
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    private sealed class Data
    {
        internal readonly HashSet<Creature> Casters = new();
        internal readonly Dictionary<CardModel, (int OriginalLevel, int Gain)> OriginalLevels = new();
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    protected override object InitInternalData() => new Data();

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        AddCaster(applier);
        if (Owner.Player?.PlayerCombatState is not { } state)
            return Task.CompletedTask;
        Data data = GetInternalData<Data>();
        foreach (CardModel card in state.AllCards.Where(card =>
            card.Owner == Owner.Player && card.CombatState == Owner.CombatState &&
            card.Pile?.Type != PileType.Deck &&
            HbrMemoryGardenCardCatalog.IsOrdinaryUpgradeCandidate(card)).ToArray())
        {
            int originalLevel = card.CurrentUpgradeLevel;
            CardCmd.Upgrade(card, CardPreviewStyle.None);
            if (card.CurrentUpgradeLevel > originalLevel)
                data.OriginalLevels[card] = (originalLevel, card.CurrentUpgradeLevel - originalLevel);
        }
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext,
        PowerModel power, decimal amount, Creature applier, CardModel cardSource)
    {
        if (power == this && amount > 0)
            AddCaster(applier);
        return Task.CompletedTask;
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented)
            return;
        Data data = GetInternalData<Data>();
        if (data.Casters.Remove(creature) && data.Casters.Count == 0)
            await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        Data data = GetInternalData<Data>();
        var combatCards = oldOwner.Player?.PlayerCombatState?.AllCards.ToHashSet()
            ?? new HashSet<CardModel>();
        foreach (var entry in data.OriginalLevels)
        {
            CardModel card = entry.Key;
            // Never restore a permanent deck entry, even if another effect has
            // moved or replaced cards while this combat benefit was active.
            if (card.Owner != oldOwner.Player || !combatCards.Contains(card) ||
                card.Pile?.Type == PileType.Deck)
                continue;
            int restoredLevel = Math.Max(0, card.CurrentUpgradeLevel - entry.Value.Gain);
            // Remove only this power's actual upgrade. Another effect may have
            // independently upgraded the same combat card since application.
            CardCmd.Downgrade(card);
            for (int level = 0; level < restoredLevel; level++)
                CardCmd.Upgrade(card, CardPreviewStyle.None);
        }
        data.OriginalLevels.Clear();
        return Task.CompletedTask;
    }

    internal void AddCaster(Creature creature)
    {
        if (creature != null)
            GetInternalData<Data>().Casters.Add(creature);
    }
}
