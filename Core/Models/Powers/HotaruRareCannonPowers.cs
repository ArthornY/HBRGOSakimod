using System;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_BREATHING_SHELTER_POWER")]
public sealed class HotaruBreathingShelterPower : HotaruUncommonCardPower
{
    [SavedProperty] public int BlockReward { get; private set; }
    [SavedProperty] public int ExtraActions { get; private set; }
    public override LocString Description
    {
        get { var text = base.Description; text.Add("Block", IsMutable ? BlockReward : 12); text.Add("ExtraActions", IsMutable ? ExtraActions : 1); return text; }
    }
    public static async Task Apply(PlayerChoiceContext context, Creature owner, int block, int extra, CardModel source)
    {
        if (!HotaruCommonCannonHelper.CanAct(owner?.Player)) return;
        var power = owner.GetPower<HotaruBreathingShelterPower>();
        int before = power?.Amount ?? 0;
        await PowerCmd.Apply<HotaruBreathingShelterPower>(context, owner, 1, owner, source);
        power = owner.GetPower<HotaruBreathingShelterPower>();
        int copies = Math.Max(0, (power?.Amount ?? 0) - before);
        if (copies == 0) return;
        power.BlockReward = BigCombatNumbers.ToNativeSigned((decimal)power.BlockReward + (decimal)block * copies);
        power.ExtraActions = BigCombatNumbers.ToNativeSigned((decimal)power.ExtraActions + (decimal)extra * copies);
    }
    internal Task OnTurnStart(PlayerChoiceContext context)
    {
        if (!HotaruCommonCannonHelper.CanAct(Owner?.Player) ||
            !Owner.CombatState.Enemies.Any(e => Millsage.IsEnemy(e, Owner.Player) && HotaruContestVulnerabilityHelper.HasAttackDown(e)) ||
            !TryClaimTurnKey("weak-start")) return Task.CompletedTask;
        var cannons = HotaruCommonCannonHelper.GetCannons(Owner.Player);
        if (cannons.Length > 0)
            Owner.Player.GetRelic<Millsage>().Charge(cannons[Owner.Player.RunState.Rng.Niche.NextInt(cannons.Length)], ExtraActions);
        return Task.CompletedTask;
    }
    public override async Task BeforeDamageReceived(PlayerChoiceContext context, Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel source)
    {
        if (target != Owner || amount <= 0m || !props.IsPoweredAttack() ||
            !Millsage.IsEnemy(dealer, Owner?.Player) || !HotaruContestVulnerabilityHelper.HasAttackDown(dealer) ||
            !TryClaimTurnKey("weak-block")) return;
        await CreatureCmd.GainBlock(Owner, BlockReward, ValueProp.Move, null);
    }
}

[CustomID("HOTARU_SIX_SENSE_REMNANTS_POWER")]
public sealed class HotaruSixSenseRemnantsPower : HotaruUncommonCardPower
{
    [SavedProperty] public int GrowthReward { get; private set; }
    [SavedProperty] public int ChargeReward { get; private set; }
    public override LocString Description
    {
        get { var text = base.Description; text.Add("BaseGain", IsMutable ? GrowthReward : 1); text.Add("Charge", IsMutable ? ChargeReward : 1); return text; }
    }
    public static async Task Apply(PlayerChoiceContext context, Creature owner, int growth, int charge, CardModel source)
    {
        if (!HotaruCommonCannonHelper.CanAct(owner?.Player)) return;
        var power = owner.GetPower<HotaruSixSenseRemnantsPower>();
        int before = power?.Amount ?? 0;
        await PowerCmd.Apply<HotaruSixSenseRemnantsPower>(context, owner, 1, owner, source);
        power = owner.GetPower<HotaruSixSenseRemnantsPower>();
        int copies = Math.Max(0, (power?.Amount ?? 0) - before);
        if (copies == 0) return;
        power.GrowthReward = BigCombatNumbers.ToNativeSigned((decimal)power.GrowthReward + (decimal)growth * copies);
        power.ChargeReward = BigCombatNumbers.ToNativeSigned((decimal)power.ChargeReward + (decimal)charge * copies);
    }
    internal async Task OnFirstEnemyDebuff(PlayerChoiceContext context, CardModel source)
    {
        if (!HotaruCommonCannonHelper.CanAct(Owner?.Player) || Amount <= 0) return;
        int charge = ChargeReward;
        int growth = GrowthReward;
        var cannons = HotaruCommonCannonHelper.GetCannons(Owner.Player);
        await PowerCmd.Apply<ChargePower>(context, Owner, charge, Owner, source);
        foreach (var cannon in cannons) await HotaruUncommonCannonHelper.Grow(context, cannon, growth, source);
    }
}
