using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;
public sealed class AoiInvincibleShieldPower : PowerModel, ICustomPower
{
    [SavedProperty] public bool TwicePerTurn { get; private set; }
    [SavedProperty] public bool GrantsAttackUp { get; private set; }
    private Dictionary<(int, MegaCrit.Sts2.Core.Combat.CombatSide, object), int> _decisions = new();
    private static readonly object OrdinaryTurn = new();
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public string CustomPackedIconPath => "res://images/powers/hbr_attack_up_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    public override LocString Description
    {
        get { LocString text = base.Description; text.Add("AoiChecks", TwicePerTurn ? 2 : 1); return text; }
    }
    internal void Configure(PeacefulMemoriesHbrgoSaki source)
    { TwicePerTurn |= source.HasUpgradeEffect(PeacefulMemoriesHbrgoSaki.BattlefieldFlowers); GrantsAttackUp |= source.HasUpgradeEffect(PeacefulMemoriesHbrgoSaki.EssenceOfProtection); Owner?.InvokePowerModified(this, 0, true); }
    internal bool TryTakeDecision()
    {
        var key = (Owner.CombatState.RoundNumber, Owner.CombatState.CurrentSide, OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player) ?? OrdinaryTurn);
        _decisions.TryGetValue(key, out int used);
        if (used >= (TwicePerTurn ? 2 : 1)) return false;
        _decisions[key] = used + 1;
        return true;
    }
    protected override void AfterCloned() { base.AfterCloned(); _decisions = new(); }
    public override Task BeforeDamageReceived(PlayerChoiceContext context, Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel source) => target == Owner
            ? ThirtyOneBEnemyActionHelper.ApplyPending(context, target) : Task.CompletedTask;
}
public sealed class AoiMiracleShieldPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public string CustomPackedIconPath => "res://images/powers/hbr_attack_up_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    public override Task BeforeDamageReceived(PlayerChoiceContext context, Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel source) => target == Owner
            ? ThirtyOneBEnemyActionHelper.ApplyPending(context, target) : Task.CompletedTask;
}
