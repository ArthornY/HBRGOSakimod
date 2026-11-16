using System;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HBR_ENERGY_DEBT_POWER")]
public sealed class HbrEnergyDebtPower : PowerModel, ICustomPower
{
    [SavedProperty] public int AppliedOwnTurn { get; private set; }
    [SavedProperty] public int NewDebtThisTurn { get; private set; }
    private object _appliedExtraTurn;
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public string CustomPackedIconPath => "res://images/powers/hbr_energy_drain_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    internal void RegisterNewCharge(Player player, int amount)
    {
        var turn = ExtraServingCombatHelper.Turn(player);
        bool same = AppliedOwnTurn == turn.Item1 && ReferenceEquals(_appliedExtraTurn, turn.Item2);
        NewDebtThisTurn = (int)Math.Min(Amount, (long)(same ? NewDebtThisTurn : 0) + Math.Max(0, amount));
        AppliedOwnTurn = turn.Item1; _appliedExtraTurn = turn.Item2;
    }
    internal int NewDebtInTurn(Player player)
    {
        var turn = ExtraServingCombatHelper.Turn(player);
        return AppliedOwnTurn == turn.Item1 && ReferenceEquals(_appliedExtraTurn, turn.Item2) ? Math.Min(Amount, NewDebtThisTurn) : 0;
    }
    public override Task AfterEnergyReset(Player player) => player == Owner?.Player
        ? ExtraServingCombatHelper.AfterEnergyReset(player) : Task.CompletedTask;
    public override LocString Description
    {
        get
        {
            LocString text = new("powers", Id.Entry + (IsMutable ? ".description" : ".referenceDescription"));
            int value = IsMutable ? Math.Max(0, Amount) : 1;
            text.Add("Debt", value);
            string energy = IsMutable ? $"[img]res://images/packed/sprite_fonts/{EnergyIconHelper.GetPrefix(this)}_energy_icon.png[/img]" : "[E]";
            text.Add("EnergyIcons", value <= 12 ? string.Concat(Enumerable.Repeat(energy, value)) : energy + "×" + value);
            return text;
        }
    }
}
