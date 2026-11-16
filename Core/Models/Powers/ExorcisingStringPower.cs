using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("EXORCISING_STRING_POWER")]
public sealed class ExorcisingStringPower : PowerModel, ICustomPower
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int UpgradeMask { get; private set; }
    private ExorcisingStringResoundHbrgoSaki _source;
    private HashSet<(int, object)> _turns = new();
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    internal bool Has(int effect) => (UpgradeMask & (1 << effect)) != 0;
    internal void RegisterSource(ExorcisingStringResoundHbrgoSaki card)
    {
        AssertMutable(); _source = card;
        foreach (int effect in card.UpgradeEffectOrder.Take(card.CurrentUpgradeLevel)) UpgradeMask |= 1 << effect;
    }
    internal ExorcisingStringResoundHbrgoSaki Source()
    {
        if (_source != null) return _source;
        _source = (ExorcisingStringResoundHbrgoSaki)ModelDb.Card<ExorcisingStringResoundHbrgoSaki>().ToMutable();
        _source.Owner = Owner.Player;
        return _source;
    }
    internal bool ClaimTurn() => _turns.Add((Owner.Player.PlayerCombatState?.TurnNumber ?? Owner.CombatState.RoundNumber,
        OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player)));
    public override Task AfterEnergyReset(Player player) => player == Owner.Player
        ? KiryuExorcisingStringCombatHelper.TurnStart(new ThrowingPlayerChoiceContext(), this) : Task.CompletedTask;
    public override LocString Description
    {
        get
        {
            var text = new LocString("powers", Id.Entry + (!IsMutable ? ".referenceDescription" : ".description"));
            text.Add("ExtraEffects", !IsMutable ? string.Empty : string.Join("\n", Enumerable.Range(1, 3).Where(Has)
                .Select(effect => new LocString("powers", Id.Entry + ".effect." + effect).GetFormattedText())));
            return text;
        }
    }
    protected override void AfterCloned() { base.AfterCloned(); _source = null; _turns = new(); }
}
