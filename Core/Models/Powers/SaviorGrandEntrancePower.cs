using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SaviorGrandEntrancePower : PowerModel, ICustomPower
{
    private int _allEnemyStunTurn = -1;
    private object _ordinaryStunToken = new();
    private HashSet<object> _allEnemyStunUses = new();
    internal bool TryTakeAllEnemyStun()
    {
        if (!HasUpgradeEffect(3) || Owner?.Player is not { } player) return false;
        int turn = player.PlayerCombatState?.TurnNumber ?? 0;
        if (_allEnemyStunTurn != turn)
        {
            _allEnemyStunTurn = turn;
            _allEnemyStunUses.Clear();
        }
        // Inserted turns have their own use; returning to the interrupted normal
        // turn must retain its earlier use rather than resetting a last-token flag.
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(player) ?? _ordinaryStunToken;
        return _allEnemyStunUses.Add(token);
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffects { get; private set; } = Array.Empty<int>();

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override Color AmountLabelColor => new("F8D36A");
    public string CustomPackedIconPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/savior_grand_entrance_hbrgo_saki.png");
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            string[] effects = UpgradeEffects.Where(id => id != 2)
                .Select(id =>
                {
                    string key = "SAVIOR_GRAND_ENTRANCE_HBRGO_SAKI.upgrade_effect." + id;
                    if (HbrBattleStatsHelper.IsEnabled(Owner) && new LocString("cards", key + ".hbr").Exists()) key += ".hbr";
                    return new LocString("cards", key).GetFormattedText();
                }).ToArray();
            description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
            return description;
        }
    }
    internal bool HasUpgradeEffect(int id) => UpgradeEffects.Contains(id);
    internal void RegisterCardSource(SaviorGrandEntranceHbrgoSaki card)
    {
        if (card == null) return;
        AssertMutable();
        UpgradeEffects = UpgradeEffects.Concat(Enumerable.Range(0, 8).Where(card.HasUpgradeEffect)).Distinct().OrderBy(id => id).ToArray();
        Owner?.InvokePowerModified(this, 0, true);
    }
    public override Task AfterApplied(Creature applier, CardModel source)
    {
        if (source is SaviorGrandEntranceHbrgoSaki card) RegisterCardSource(card);
        return Task.CompletedTask;
    }
    public override Task AfterDamageGiven(PlayerChoiceContext context, Creature dealer,
        DamageResult result, ValueProp props, Creature target, CardModel cardSource) =>
        SaviorGrandEntranceCombatHelper.AfterDamageGiven(context, this, dealer, result, props, target, cardSource);
    protected override void AfterCloned()
    {
        base.AfterCloned();
        UpgradeEffects = UpgradeEffects?.ToArray() ?? Array.Empty<int>();
        _allEnemyStunTurn = -1;
        _ordinaryStunToken = new();
        _allEnemyStunUses = new();
    }
}
