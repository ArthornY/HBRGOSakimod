using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("MUUA_I_WILL_NOT_FALL_POWER")]
public sealed class MuuaIWillNotFallPower : PowerModel, ICustomPower
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int UpgradeMask { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool DpRescueUsed { get; private set; }
    private IWillNotFallHbrgoSaki _source;
    private HashSet<(int, object)> _turns = new();
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    internal bool Has(int effect) => (UpgradeMask & (1 << effect)) != 0;
    internal void RegisterSource(IWillNotFallHbrgoSaki card)
    {
        AssertMutable(); _source = card;
        foreach (int effect in card.UpgradeEffectOrder.Take(card.CurrentUpgradeLevel)) UpgradeMask |= 1 << effect;
        Owner?.InvokePowerModified(this, 0, true);
    }
    internal IWillNotFallHbrgoSaki Source()
    {
        if (_source != null) return _source;
        _source = Owner.Player?.Deck?.Cards.OfType<IWillNotFallHbrgoSaki>().FirstOrDefault();
        if (_source == null) { _source = (IWillNotFallHbrgoSaki)ModelDb.Card<IWillNotFallHbrgoSaki>().ToMutable(); _source.Owner = Owner.Player; }
        return _source;
    }
    internal bool ClaimTurn()
    {
        var player = Owner.Player;
        return _turns.Add((player.PlayerCombatState?.TurnNumber ?? Owner.CombatState.RoundNumber,
            OverclockImmediateTurnCoordinator.GetTurnToken(player)));
    }
    internal bool ClaimRescue()
    {
        if (DpRescueUsed || !Has(IWillNotFallHbrgoSaki.NeverFall)) return false;
        DpRescueUsed = true; return true;
    }
    public override Task AfterApplied(Creature applier, CardModel cardSource)
    { if (cardSource is IWillNotFallHbrgoSaki card) RegisterSource(card); return Task.CompletedTask; }
    public override Task AfterEnergyReset(Player player) => player == Owner.Player
        ? MuuaFaithCombatHelper.TurnStart(new ThrowingPlayerChoiceContext(), this) : Task.CompletedTask;
    public override LocString Description
    {
        get
        {
            LocString text = new("powers", Id.Entry + (!IsMutable ? ".referenceDescription" : ".description"));
            text.Add("UpgradeEffects", !IsMutable ? string.Empty : string.Join("\n", Enumerable.Range(1, 6)
                .Where(Has).Select(effect => new LocString("powers", Id.Entry + ".effect." + effect).GetFormattedText())));
            return text;
        }
    }
    protected override void AfterCloned() { base.AfterCloned(); _source = null; _turns = new(); }
}
