using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards.Mocks;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Capabilities;

public sealed class SixSenseCardCapability :
    CardCapability,
    ICardHoverTipContributor
{
    private SixSenseAttribute? _attribute;
    private readonly Stack<PlayState> _plays = new();

    public SixSenseAttribute? BaseAttribute => _attribute;

    internal static SixSenseAttribute[] GetCandidates(CardType type) => type switch
    {
        CardType.Attack => new[] { SixSenseAttribute.Madness, SixSenseAttribute.LeadershipCharm },
        CardType.Skill => new[] { SixSenseAttribute.Simplicity, SixSenseAttribute.MentalStrength },
        CardType.Power => new[] { SixSenseAttribute.Talent, SixSenseAttribute.Gentleness },
        _ => Array.Empty<SixSenseAttribute>()
    };

    internal static void AssignIfMissing(CardModel card, Player player)
        => AssignUsingRng(card, player?.PlayerRng?.Rewards);

    internal static void PreviewIfMissing(CardModel card, Player player)
    {
        if (player?.PlayerRng == null) return;

        // Each alternative predicts the next normal acquisition from a separate
        // snapshot. Never advance or restore the player's actual reward stream.
        PlayerRngSet snapshot = PlayerRngSet.FromSerializable(player.PlayerRng.ToSerializable());
        AssignUsingRng(card, snapshot.Rewards);
    }

    private static void AssignUsingRng(CardModel card, Rng rewards)
    {
        if (card?.IsMutable != true || card is MockCardModel or PhantomChoiceHbrgoSaki
                or PhantomFusionEffectChoiceHbrgoSaki or ReptureBowChoiceHbrgoSaki
                or ReptureDualSwordsChoiceHbrgoSaki or RaptureScytheChoiceHbrgoSaki ||
            rewards == null ||
            GetCandidates(card.Type).Length == 0)
        {
            return;
        }

        SixSenseCardCapability capability = card.Capability<SixSenseCardCapability>();
        if (capability == null || capability._attribute.HasValue)
        {
            return;
        }

        SixSenseAttribute[] candidates = GetCandidates(card.Type);
        capability._attribute = candidates[rewards.NextInt(candidates.Length)];
        capability.MarkDirty();
    }

    internal static IReadOnlyList<SixSenseAttribute> GetAttributes(CardModel card)
    {
        return card?.IsMutable == true && card.Capability<SixSenseCardCapability>()?._attribute is { } primary
            ? new[] { primary }
            : Array.Empty<SixSenseAttribute>();
    }

    internal static IReadOnlyList<SixSenseAttribute> GetResistanceAttributes(CardModel card)
    {
        if (card?.IsMutable != true || card.Capability<SixSenseCardCapability>()?._attribute is not { } primary)
        {
            return Array.Empty<SixSenseAttribute>();
        }

        ElectronicSoldierManual manual = card.Owner?.GetRelic<ElectronicSoldierManual>();
        return ElectronicSoldierManual.AllSixSenseAttributes
            .Where(attribute => attribute == primary || manual?.HasSixSenseBonus(attribute) == true)
            .ToArray();
    }

    internal static int GetRollBonus(CardModel card)
    {
        ElectronicSoldierManual manual = card?.IsMutable == true
            ? card.Owner?.GetRelic<ElectronicSoldierManual>()
            : null;
        if (manual == null || manual.IsHbrNumericsEnabled)
        {
            return 0;
        }

        decimal bonus = GetAttributes(card).Sum(attribute => Math.Max(0m, manual.GetEffectiveSenseAmount(attribute)));
        return (int)Math.Min(int.MaxValue, decimal.Floor(bonus));
    }

    internal static decimal GetMadnessDamageMultiplier(CardModel card)
    {
        if (!GetAttributes(card).Contains(SixSenseAttribute.Madness)) return 1m;
        return SixSenseCombatHelper.EffectValue(card.Owner, SixSenseAttribute.Madness);
    }

    internal static int GetTriggeredRollBonus(CardModel card, bool preview = false)
    {
        SixSenseCombatPower power = card?.Owner?.Creature?.GetPower<SixSenseCombatPower>();
        if (power == null) return 0;
        SixSenseCardCapability capability = card.Capability<SixSenseCardCapability>();
        decimal leadership = capability?._plays.Count > 0
            ? capability._plays.Peek().LeadershipBonus
            : preview && card.Type != CardType.Attack ? power.PendingLeadershipBonus : 0m;
        return (int)Math.Min(int.MaxValue, decimal.Floor(power.CombatRollBonus + leadership));
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay) => PreparePlay(cardPlay);

    internal async Task PreparePlay(CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay?.Card, Owner) || _plays.Any(state => ReferenceEquals(state.Play, cardPlay)))
            return;
        decimal leadership = Owner.Type != CardType.Attack
            ? Owner.Owner?.Creature?.GetPower<SixSenseCombatPower>()?.TakeLeadershipBonus() ?? 0m
            : 0m;
        _plays.Push(new PlayState(cardPlay, GetAttributes(Owner).ToArray(),
            GetResistanceAttributes(Owner).ToArray(), leadership));
        if (Owner.Owner?.Creature?.GetPower<SixSenseCombatPower>() is { } state)
            await state.SyncEffectPowers(new ThrowingPlayerChoiceContext());
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_plays.Count == 0 || !ReferenceEquals(_plays.Peek().Play, cardPlay)) return;
        PlayState state = _plays.Pop();
        await SixSenseCombatHelper.ResolveCardEffects(choiceContext, cardPlay,
            state.Attributes, state.ResistanceAttributes);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _plays.Clear();
        return Task.CompletedTask;
    }

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        IReadOnlyList<SixSenseAttribute> attributes = GetAttributes(card);
        if (attributes.Count == 0)
        {
            yield break;
        }

        string names = string.Join("、", GetResistanceAttributes(card).Select(ElectronicSoldierManual.GetSenseTitle));
        string primary = ElectronicSoldierManual.GetSenseTitle(_attribute.Value);
        ElectronicSoldierManual manual = card.Owner?.GetRelic<ElectronicSoldierManual>();
        bool hbrNumerics = manual == null || manual.IsHbrNumericsEnabled;
        LocString description = new("card_keywords", "HBRGO_SIX_SENSE." +
            (hbrNumerics ? "hbrDescription" : "description"));
        description.Add("primary", primary);
        description.Add("attributes", names);
        if (hbrNumerics)
        {
            description.Add("statName", HbrBattleStatsHelper.GetBattleStatTitle(_attribute.Value));
            description.Add("statBonus", Math.Max(0m, manual?.GetEffectiveSenseAmount(_attribute.Value) ?? 0m) *
                HbrBattleStatsHelper.SenseStatGainPerLevel);
        }
        else description.Add("bonus", GetRollBonus(card));
        yield return new HoverTip(new LocString("card_keywords", "HBRGO_SIX_SENSE.title"), description);
        foreach (SixSenseAttribute attribute in attributes)
        {
            bool skillBlock = hbrNumerics && attribute == SixSenseAttribute.Simplicity;
            LocString effect = new("card_keywords", $"HBRGO_SIX_SENSE.{attribute}." +
                (hbrNumerics && attribute is SixSenseAttribute.Simplicity or SixSenseAttribute.Madness
                    ? "hbrDescription" : "description"));
            decimal value = SixSenseCombatHelper.EffectValue(card.Owner, attribute);
            if (skillBlock)
            {
                effect.Add("skill", HbrSkillPower.BlockPercent(value).ToString("0.##", CultureInfo.InvariantCulture));
                effect.Add("value", HbrSkillBlockHelper.ResolveUnpowered(card.Owner?.Creature, value));
            }
            else effect.Add("value", attribute == SixSenseAttribute.Madness ? value * 100m : value);
            effect.Add("loss", RollKeywordRules.IsCopiedCard(card) ? 0 : SixSenseCombatHelper.ResistanceLossForPlayer(card.Owner, attribute));
            effect.Add("state", SixSenseCombatHelper.IsEnhanced(card.Owner, attribute) ? "强化" : "普通");
            yield return new HoverTip(new LocString("card_keywords", $"HBRGO_SIX_SENSE.{attribute}.title"), effect);
        }
    }

    protected override JsonNode SaveAdditionalState() => new JsonObject
    {
        ["attribute"] = _attribute.HasValue ? (int)_attribute.Value : -1
    };

    protected override void LoadAdditionalState(JsonNode state, int schemaVersion)
    {
        int value = state?["attribute"]?.GetValue<int>() ?? -1;
        _attribute = Enum.IsDefined(typeof(SixSenseAttribute), value)
            ? (SixSenseAttribute)value
            : null;
    }

    private sealed record PlayState(CardPlay Play, SixSenseAttribute[] Attributes,
        SixSenseAttribute[] ResistanceAttributes, decimal LeadershipBonus);
}
