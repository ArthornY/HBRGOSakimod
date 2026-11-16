using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public abstract class ThirtyAClassRelic : RelicModel, IThirtyAClassRelic
{
    internal bool IsActive => IsMutable && !IsMelted && Owner?.Character is HbrgoSaki30A && HbrBattleStatsHelper.IsEnabled(Owner);
    public override string PackedIconPath => ImageHelper.GetImagePath("relics/restoration_moment.png");
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => PackedIconPath;
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            foreach (string key in new[] { "phantom", "roster", "experience", "revival" })
                yield return new HoverTip(new LocString("relics", "THIRTY_A_CLASS_RELICS." + key + ".title"),
                    new LocString("relics", "THIRTY_A_CLASS_RELICS." + key + ".description"));
        }
    }
}

public sealed class LetterArrivesThirtyA : ThirtyAClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool UsedThisCombat { get; private set; }
    public override Task BeforeCombatStart() { UsedThisCombat = false; return Task.CompletedTask; }
    internal async Task AfterPhantomActed(Creature phantom)
    {
        if (!IsActive || UsedThisCombat || !PhantomHelper.IsOwnedMainPhantom(phantom, Owner)) return;
        UsedThisCombat = true;
        Flash();
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1m, Owner);
    }
}

public sealed class FormerFoesThirtyA : ThirtyAClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
}

public sealed class CheckmateThirtyA : ThirtyAClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    internal sealed class TurnUse { internal bool Used, Armed; }
    private ConditionalWeakTable<object, TurnUse> _extra = new();
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public int OrdinaryTurn { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public bool OrdinaryUsed { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public bool OrdinaryArmed { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public bool EnemyUsed { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public bool EnemyArmed { get; private set; }
    private bool IsEnemyTurn => Owner.Creature?.CombatState?.CurrentSide == CombatSide.Enemy;
    private void SyncOrdinary()
    {
        int turn = Owner.PlayerCombatState?.TurnNumber ?? 0;
        if (OrdinaryTurn == turn) return;
        OrdinaryTurn = turn; OrdinaryUsed = OrdinaryArmed = false;
    }
    private TurnUse Extra(bool create)
    {
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner);
        return token == null ? null : create ? _extra.GetOrCreateValue(token) : _extra.TryGetValue(token, out var value) ? value : null;
    }
    internal bool TryConsumeAttack()
    {
        if (!IsActive) return false;
        if (OverclockImmediateTurnCoordinator.GetTurnToken(Owner) != null)
        {
            TurnUse extra = Extra(false);
            if (extra?.Armed != true) return false;
            extra.Armed = false; return true;
        }
        if (IsEnemyTurn) { bool result = EnemyArmed; EnemyArmed = false; return result; }
        SyncOrdinary(); bool armed = OrdinaryArmed; OrdinaryArmed = false; return armed;
    }
    public override Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play)
    {
        if (!IsActive || play?.Card?.Owner != Owner || play.Card.Type != CardType.Attack) return Task.CompletedTask;
        if (OverclockImmediateTurnCoordinator.GetTurnToken(Owner) != null)
        {
            TurnUse extra = Extra(true);
            if (!extra.Used) { extra.Used = extra.Armed = true; Flash(); }
        }
        else if (IsEnemyTurn)
        {
            if (!EnemyUsed) { EnemyUsed = EnemyArmed = true; Flash(); }
        }
        else
        {
            SyncOrdinary();
            if (!OrdinaryUsed) { OrdinaryUsed = OrdinaryArmed = true; Flash(); }
        }
        return Task.CompletedTask;
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Enemy) { OrdinaryArmed = false; EnemyUsed = EnemyArmed = false; }
        else if (participants.Contains(Owner.Creature)) EnemyArmed = false;
        return Task.CompletedTask;
    }
    public override Task BeforeCombatStart()
    {
        OrdinaryTurn = 0; OrdinaryUsed = OrdinaryArmed = EnemyUsed = EnemyArmed = false; _extra = new();
        return Task.CompletedTask;
    }
    protected override void AfterCloned() { base.AfterCloned(); _extra = new(); }
}

public sealed class TicketStubThirtyA : ThirtyAClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
}

public sealed class PhantomsOfBygoneDaysThirtyA : ThirtyAClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public int PendingRewards { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public bool WaitingForEnergyReset { get; private set; }
    public override bool ShowCounter => IsMutable && PendingRewards > 0;
    public override int DisplayAmount => IsMutable ? PendingRewards : 0;
    internal void BeforeOwnTurnReset() { if (IsActive) WaitingForEnergyReset = true; }
    internal async Task AfterPhantomRevived(Creature phantom)
    {
        if (!IsActive || !PhantomHelper.IsOwnedMainPhantom(phantom, Owner) || !Owner.Creature.IsAlive) return;
        if (WaitingForEnergyReset || phantom.CombatState?.CurrentSide != Owner.Creature.Side)
        {
            PendingRewards = (int)Math.Min(int.MaxValue, (long)PendingRewards + 1); InvokeDisplayAmountChanged();
            return;
        }
        await GiveRewards(1);
    }
    private async Task GiveRewards(int count)
    {
        if (count <= 0 || !IsActive || !Owner.Creature.IsAlive) return;
        Flash();
        await PlayerCmd.GainEnergy(count, Owner);
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), count, Owner);
    }
    private Task Drain()
    {
        WaitingForEnergyReset = false;
        int count = PendingRewards; PendingRewards = 0; InvokeDisplayAmountChanged();
        return GiveRewards(count);
    }
    public override Task AfterEnergyReset(Player player) => player == Owner ? Drain() : Task.CompletedTask;
    internal static Task AfterInsertedEnergyReset(Player player) => player?.GetRelic<PhantomsOfBygoneDaysThirtyA>()?.Drain() ?? Task.CompletedTask;
    public override Task BeforeCombatStart() { PendingRewards = 0; WaitingForEnergyReset = true; return Task.CompletedTask; }
    public override Task AfterCombatEnd(CombatRoom room) { PendingRewards = 0; WaitingForEnergyReset = false; return Task.CompletedTask; }
}

public sealed class ShelterRationCouponThirtyA : ThirtyAClassRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    public override Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent)
    {
        if (IsActive && player == Owner && itemPurchased != null && Owner.GetRelic<MomentOfReign>() is { } reign && reign.GrantLastSummonedOrdinaryExperience()) Flash();
        return Task.CompletedTask;
    }
}
