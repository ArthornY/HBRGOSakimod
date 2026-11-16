using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NararaPressurePower : PowerModel, ICustomPower
{
    private ConditionalWeakTable<object, object> _actions = new();
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Pressure { get; private set; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    // Native Amount stays one: a zero-pressure ability remains installed.
    public override int DisplayAmount => Pressure;
    public string CustomPackedIconPath => "res://images/powers/narara_sleeping_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal void ResetPressure()
    {
        Pressure = 0;
        _actions = new();
        InvokeDisplayAmountChanged();
    }
    protected override void AfterCloned()
    { base.AfterCloned(); _actions = new(); }

    internal async Task RecordHit(PlayerChoiceContext context, Creature dealer, CardModel card, ValueProp props = ValueProp.Move)
    {
        if (Owner?.IsAlive != true || Owner.Monster is not NararaShibaBossMonster boss ||
            !boss.UsesAnkoRules || (boss.ExactDpLayerMaximum > 0m && boss.DpLayersRemaining == 0) || dealer?.Side == Owner.Side || dealer == null ||
            Owner.GetPower<NararaSleepingPower>() is not { Amount: > 0 } ||
            NararaNullificationHelper.IsCardNullified(Owner, card)) return;
        object token = NararaPressureActionHelper.Token(Owner, dealer, card, props);
        if (token == null || _actions.TryGetValue(token, out _)) return;
        _actions.Add(token, new object());
        Pressure = Math.Min(7, Pressure + 1);
        InvokeDisplayAmountChanged();
        Flash();
        if (Pressure >= 7) await boss.WakeFromPressure(context);
    }

    public override Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result,
        ValueProp props, Creature dealer, CardModel cardSource) => target == Owner &&
        result.BlockedDamage + result.UnblockedDamage > 0m
            ? RecordHit(context, dealer, cardSource, props) : Task.CompletedTask;
}
