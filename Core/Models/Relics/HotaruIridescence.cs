using System;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

[CustomID("HOTARU_IRIDESCENCE")]
public sealed class HotaruIridescence : RelicModel
{
    public const int MaximumLayers = 6;
    private int _layers = 1;

    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool ShowCounter => true;
    public override int DisplayAmount => Layers;
    public override string PackedIconPath => "res://images/relics/hotaru_iridescence.svg";
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => PackedIconPath;

    [SavedProperty]
    public int Layers
    {
        get => _layers;
        private set
        {
            AssertMutable();
            int next = Math.Clamp(value, 1, MaximumLayers);
            if (_layers == next) return;
            _layers = next;
            InvokeDisplayAmountChanged();
        }
    }

    public int RollBonus => Layers * 30;
    internal int TurnEnergy => Layers >= 4 ? Layers - 2 : 0;
    internal int StartingStats => Layers >= 5 ? Layers * Layers : 0;

    internal bool GainCharge()
    {
        if (Layers >= MaximumLayers) return false;
        Layers++;
        Flash();
        return true;
    }

    internal void AddDescriptionVariables(LocString description)
    {
        description.Add("RollBonus", RollBonus);
        description.Add("StartingStats", StartingStats > 0 ? StartingStats : 25);
        // Use native energy sprites directly so this variable also works in
        // relic hover tips, where card-only SimpleLoc tokens are not expanded.
        string prefix = EnergyIconHelper.GetPrefix(this);
        string icon = $"[img]res://images/packed/sprite_fonts/{prefix}_energy_icon.png[/img]";
        description.Add("EnergyIcons", string.Concat(Enumerable.Repeat(icon, Math.Max(2, TurnEnergy))));
    }

    public override async Task BeforeCombatStart()
    {
        if (StartingStats <= 0 || Owner?.Creature is not { IsAlive: true, CombatState: not null } creature)
            return;
        Flash();
        var context = new BlockingPlayerChoiceContext();
        await PowerCmd.Apply<StrengthPower>(context, creature, StartingStats, creature, null);
        await PowerCmd.Apply<DexterityPower>(context, creature, StartingStats, creature, null);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player != Owner || Layers < 2 || Owner?.Creature is not { IsAlive: true, CombatState: not null })
            return;
        Flash();
        if (TurnEnergy > 0) await PlayerCmd.GainEnergy(TurnEnergy, Owner);
        await CardPileCmd.Draw(context, 2m, Owner);
    }
}
