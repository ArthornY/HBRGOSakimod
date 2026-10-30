using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class DoublePowersHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/double_powers_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public DoublePowersHbrgoSaki()
        : base(
            3,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        // Snapshot all amounts before callbacks can modify another power.
        var powers = Owner.Creature.CombatState
            .Allies
            .Concat(Owner.Creature.CombatState.Enemies)
            .SelectMany(creature => creature.Powers)
            .Where(power =>
                power is not HighDimensionalShellPower &&
                power is not PhaseWarpPower &&
                power is not ShieldLockPower)
            .Select(power => (Power: power, Amount: power is IExactDamagePercentPower exact
                    ? exact.PercentState.GetPercent(power.Amount) : (decimal)power.Amount,
                DuplicateCounters: PowerDuplicationBookkeepingHelper.SnapshotCounters(power),
                DebuffPercent: HbrPercentDebuffHelper.IsEnabled(power)
                    ? (decimal?)HbrPercentDebuffHelper.GetEffectPercent(power) : null))
            .ToArray();
        foreach (var (power, amount, duplicateCounters, debuffPercent) in powers)
        {
            // Refund/expiry bookkeeping must also double when a permanent and
            // temporary stat cancel out, leaving no visible stat power.
            duplicateCounters?.Invoke();
            if (amount == 0)
                continue;

            if (debuffPercent.HasValue)
            {
                await HbrPercentDebuffApplication.DoubleExisting(choiceContext, power,
                    debuffPercent.Value, (int)amount, Owner.Creature, this);
                continue;
            }

            // The matching total stat is doubled separately from this snapshot.
            using TemporaryStatDuplicationScope scope =
                TemporaryStatDuplicationScope.Begin(power, this);
            await PowerCmd.ModifyAmount(
                choiceContext,
                power,
                amount,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
