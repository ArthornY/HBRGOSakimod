using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class Millsage
{
    private int _retiredCannonsMask;
    private int _cannonRetirementChoices;
    private int _endgameOfferMask;
    private int _firstRetiredCannon = -1;
    private decimal _permanentSacrificeDamagePercent;
    private static readonly MethodInfo PetDiedHandler = AccessTools.DeclaredMethod(typeof(PlayerCombatState), "OnPetDied");

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RetiredCannonsMask
    {
        get => _retiredCannonsMask;
        private set { AssertMutable(); _retiredCannonsMask = Math.Clamp(value, 0, 31); }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int EndgameOfferMask
    {
        get => _endgameOfferMask;
        private set { AssertMutable(); _endgameOfferMask = Math.Clamp(value, 0, 31); }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool EndgameOffersDeclined { get; private set; }

    [SavedProperty]
    public int FirstRetiredCannon
    {
        get => _firstRetiredCannon;
        private set { AssertMutable(); _firstRetiredCannon = Math.Clamp(value, -1, 4); }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string PermanentSacrificeDamagePercentData
    {
        get => DamageAmplificationLimits.Format(_permanentSacrificeDamagePercent);
        private set { AssertMutable(); _permanentSacrificeDamagePercent = DamageAmplificationLimits.Parse(value); }
    }

    public decimal PermanentSacrificeDamagePercent => _permanentSacrificeDamagePercent;
    public int RetiredCannonCount => System.Numerics.BitOperations.PopCount((uint)RetiredCannonsMask);
    public int EndgameSign => Layers - (IsMutable ? Owner?.GetRelic<HotaruIridescence>()?.Layers ?? 1 : 1);
    public int EndgameSoulPercent => EndgameSign > 0 ? 80 : 50 + RetiredCannonCount * 10;
    public int EndgameStartHitCount => EndgameSign > 0 ? 1 + (WasCannonRetired(HotaruCannonKind.Absolution) ? 1 : 0)
        : EndgameSign < 0 && WasCannonRetired(HotaruCannonKind.Absolution) ? 1 : 0;
    public int EndgameEndHitCount => EndgameSign < 0 ? 1 : 0;
    public decimal EndgameBaseDamage
    {
        get
        {
            int count = RetiredCannonCount;
            if (count == 0 || EndgameSign == 0) return 0m;
            decimal value = 1m;
            if (EndgameSign > 0)
            {
                for (int factor = 2; factor <= count; factor++) value *= factor;
                return value;
            }
            int radix = WasCannonRetired(HotaruCannonKind.Happiness) ? 3 : 2;
            for (int power = 0; power < count; power++) value *= radix;
            return radix == 3 ? value - 1m : value;
        }
    }

    public HotaruCannonKind FirstRetiredCannonKind => HotaruCannonKinds.IsOriginal((HotaruCannonKind)FirstRetiredCannon) &&
        WasCannonRetired((HotaruCannonKind)FirstRetiredCannon) ? (HotaruCannonKind)FirstRetiredCannon :
        HotaruCannonKinds.Original.FirstOrDefault(WasCannonRetired);

    public bool WasCannonRetired(HotaruCannonKind kind) =>
        HotaruCannonKinds.IsOriginal(kind) && (RetiredCannonsMask & (1 << (int)kind)) != 0;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CannonRetirementChoices
    {
        get => _cannonRetirementChoices;
        private set { AssertMutable(); _cannonRetirementChoices = Math.Clamp(value, 0, 1023); }
    }

    public HotaruCannonRetirementChoice GetCannonRetirementChoice(HotaruCannonKind kind)
    {
        if (!WasCannonRetired(kind)) return HotaruCannonRetirementChoice.None;
        int choice = (CannonRetirementChoices >> ((int)kind * 2)) & 3;
        return choice is 1 or 2 ? (HotaruCannonRetirementChoice)choice : HotaruCannonRetirementChoice.None;
    }

    public bool CanOfferEndgameCard(HotaruCannonKind kind) => IsMutable && !EndgameOffersDeclined &&
        HotaruCannonKinds.IsOriginal(kind) && !WasCannonRetired(kind) && GetCannonUpgradeLevel(kind) == 7 &&
        (EndgameOfferMask & (1 << (int)kind)) == 0;

    public bool TryClaimEndgameOffer(HotaruCannonKind kind)
    {
        if (!CanOfferEndgameCard(kind)) return false;
        EndgameOfferMask |= 1 << (int)kind;
        return true;
    }

    public void ReleaseEndgameOffer(HotaruCannonKind kind)
    {
        AssertMutable();
        if (HotaruCannonKinds.IsOriginal(kind)) EndgameOfferMask &= ~(1 << (int)kind);
    }

    public void DeclineEndgameOffers() { AssertMutable(); EndgameOffersDeclined = true; }

    public bool CanRetireCannon(Creature cannon) => IsMutable && CanAct() && IsOwnedCannon(cannon, Owner) &&
        cannon.Monster is HotaruCannonModel model && IsCannonUnlocked(model.Kind) && GetCannonUpgradeLevel(model.Kind) == 7;

    public async Task<bool> RetireCannon(Creature cannon, bool sacrifice, PlayerChoiceContext context,
        CardModel source = null, CardPlay cardPlay = null)
    {
        if (!CanRetireCannon(cannon)) return false;
        HotaruIridescence rainbow = Owner.GetRelic<HotaruIridescence>();
        if (sacrifice && rainbow == null)
        {
            // Existing runs from before this starter was added can still use
            // the new card. Their implicit initial charge becomes an actual relic.
            rainbow = await RelicCmd.Obtain<HotaruIridescence>(Owner);
            if (!CanRetireCannon(cannon)) return false;
        }
        HotaruCannonKind kind = ((HotaruCannonModel)cannon.Monster).Kind;
        int currentSoul = GetCannonDamagePercent(kind);
        Creature previousEndgame = GetCannon(HotaruCannonKind.Endgame);
        bool hadEndgameCannon = previousEndgame != null;
        decimal previousEndgameDamage = HotaruMyIndulgencePower.ModifyBaseDamage(previousEndgame, EndgameBaseDamage + HotaruUncommonCannonHelper.DamageBonus(previousEndgame));
        // Retiring Absolution can turn an already active Indulgence into its
        // redemption form, increasing every surviving cannon's base damage.
        var previousOriginalDamage = HotaruCannonKinds.Original
            .Select(original => GetCannon(original))
            .Where(original => original?.Monster is HotaruCannonModel)
            .Select(original => (Kind: ((HotaruCannonModel)original.Monster).Kind,
                Damage: HotaruMyIndulgencePower.ModifyBaseDamage(original,
                    ((HotaruCannonModel)original.Monster).CurrentDamage + HotaruUncommonCannonHelper.DamageBonus(original)))).ToArray();
        // Commit the identity before awaiting effects. Copies and nested hooks
        // cannot retire this cannon twice or choose a different first slot.
        if (RetiredCannonCount == 0) FirstRetiredCannon = (int)kind;
        RetiredCannonsMask |= 1 << (int)kind;
        int choiceShift = (int)kind * 2;
        CannonRetirementChoices = (CannonRetirementChoices & ~(3 << choiceShift)) |
            ((int)(sacrifice ? HotaruCannonRetirementChoice.Sacrifice : HotaruCannonRetirementChoice.Redemption) << choiceShift);
        if (sacrifice)
        {
            rainbow.GainCharge();
            _permanentSacrificeDamagePercent = DamageAmplificationLimits.Clamp(
                DamageAmplificationLimits.AddSaturated(_permanentSacrificeDamagePercent, currentSoul));
        }
        else GainCharge();
        await DismissCannon(cannon);
        await RefreshEndgameCannon();
        if (sacrifice && currentSoul > 0 && Owner.Creature.IsAlive)
            await PowerCmd.Apply<HotaruDamageAmplificationPower>(context, Owner.Creature, currentSoul, Owner.Creature, source);
        // Rebuilding an existing terminal cannon preserves its logical growth.
        // First summoning or reappearing after a tie initializes its base value.
        var growthReceipts = new List<(Creature Cannon, decimal Increase)>();
        if (hadEndgameCannon && GetCannon(HotaruCannonKind.Endgame) is { } endgame)
            growthReceipts.Add((endgame,
                HotaruMyIndulgencePower.ModifyBaseDamage(endgame, EndgameBaseDamage + HotaruUncommonCannonHelper.DamageBonus(endgame)) - previousEndgameDamage));
        foreach (var previous in previousOriginalDamage)
            if (GetCannon(previous.Kind) is { Monster: HotaruCannonModel model } original)
                growthReceipts.Add((original,
                    HotaruMyIndulgencePower.ModifyBaseDamage(original, model.CurrentDamage + HotaruUncommonCannonHelper.DamageBonus(original)) - previous.Damage));
        foreach (var growth in growthReceipts)
            await NotifyCannonBaseDamageIncreased(context, growth.Cannon, growth.Increase, source, cardPlay);
        InvokeDisplayAmountChanged();
        CombatPreviewRefreshHelper.RefreshCombatState(Owner.Creature, nameof(RetireCannon));
        return true;
    }

    internal async Task RefreshEndgameCannon()
    {
        if (!IsMutable || Owner?.Creature?.CombatState is not { } combat || !Owner.Creature.IsAlive) return;
        Creature old = GetCannon(HotaruCannonKind.Endgame);
        if (old != null) await DismissCannon(old);
        if (RetiredCannonCount == 0 || EndgameSign == 0) return;
        Creature cannon = combat.CreateCreature(ModelDb.Monster<HotaruFinalCannon>().ToMutable(), Owner.Creature.Side, null);
        await PlayerCmd.AddPet(cannon, Owner);
        cannon.PrepareForNextTurn(combat.Enemies);
        await RefreshIntent(cannon);
    }

    private async Task DismissCannon(Creature cannon)
    {
        PlayerCombatState state = Owner.PlayerCombatState;
        // Escape has no death rewards. Native pet bookkeeping only exposes a
        // death callback, so detach that listener/list entry when dismissing.
        if (state?.Pets is IList<Creature> pets && pets.Contains(cannon))
        {
            if (PetDiedHandler != null) cannon.Died -= PetDiedHandler.CreateDelegate<Action<Creature>>(state);
            pets.Remove(cannon);
        }
        await CreatureCmd.Escape(cannon);
    }

    private bool HasPhaseEffect(HotaruCannonKind kind, HotaruCannonPhase phase) => kind == HotaruCannonKind.Endgame
        ? (phase == HotaruCannonPhase.Start ? EndgameStartHitCount : EndgameEndHitCount) > 0
        : HotaruCannonState.HasEffect(kind, phase);

    public override decimal ModifyBlockMultiplicative(Creature target, decimal amount, ValueProp props,
        CardModel cardSource, CardPlay cardPlay) =>
        target == Owner?.Creature || IsOwnedCannon(target, Owner)
            ? HotaruEndgameEffectsHelper.GetBlockMultiplier(target) : 1m;
}
