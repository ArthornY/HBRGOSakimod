using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HextechRunes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.HextechCompat;

public abstract class HbrgoSaki31OForge : HextechForgeBase
{
    protected int EffectiveStackCount => Math.Max(1, StackCount);

    public sealed override bool IsAvailableForPlayer(Player player)
    {
        return MainModBridge.Is31OPlayer(player);
    }
}

public abstract class HbrgoSaki31ORune : HextechRelicBase
{
    public sealed override bool IsAvailableForPlayer(Player player)
    {
        return MainModBridge.Is31OPlayer(player);
    }
}

public sealed class DoubleSwordsDoubleRune : HbrgoSaki31ORune
{
}

public sealed class ChangingSkyAspectRune : HbrgoSaki31ORune
{
    private int _freeBowCardUsedRound = int.MinValue;

    [SavedProperty]
    public int FreeBowCardUsedRound
    {
        get => _freeBowCardUsedRound;
        private set
        {
            AssertMutable();
            _freeBowCardUsedRound = value;
        }
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (Owner == null ||
            card?.Owner != Owner ||
            card.EnergyCost.CostsX ||
            card.Pile?.Type is not (PileType.Hand or PileType.Play) ||
            !MainModBridge.HasBowMode(Owner) ||
            !MainModBridge.IsBowFormCard(card) ||
            GetRoundNumber() == FreeBowCardUsedRound)
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Owner == null ||
            cardPlay?.Card?.Owner != Owner ||
            cardPlay.IsAutoPlay ||
            !cardPlay.IsFirstInSeries ||
            !MainModBridge.HasBowMode(Owner) ||
            !MainModBridge.IsBowFormCard(cardPlay.Card))
        {
            return Task.CompletedTask;
        }

        int round = GetRoundNumber();
        if (round == FreeBowCardUsedRound)
        {
            return Task.CompletedTask;
        }

        FreeBowCardUsedRound = round;
        Flash();
        return Task.CompletedTask;
    }

    private int GetRoundNumber()
    {
        return Owner?.Creature?.CombatState?.RoundNumber ?? int.MinValue;
    }
}

public sealed class AnokoRune : HbrgoSaki31ORune
{
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        if (Owner == null)
        {
            return;
        }

        Flash();
        await MainModBridge.TransformChihayaToAnoko(Owner);
    }
}

public sealed class SuperTropicalHellRune : HbrgoSaki31ORune
{
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        if (Owner == null)
        {
            return;
        }

        Flash();
        await MainModBridge.TransformCurseAndStatusCardsToEternalYo(Owner);
    }

    public override bool TryModifyCardBeingAddedToDeck(
        CardModel card,
        out CardModel newCard)
    {
        newCard = null;
        if (Owner == null ||
            card?.Owner != Owner ||
            card.Type is not (CardType.Curse or CardType.Status))
        {
            return false;
        }

        newCard = MainModBridge.CreateEternalYo(Owner);
        if (newCard == null)
        {
            return false;
        }

        Flash();
        return true;
    }
}

public sealed class ActuallyCancerRune : HbrgoSaki31ORune
{
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        if (Owner == null)
        {
            return;
        }

        Flash();
        await MainModBridge.SwapMaxHpAndDp(Owner);
    }
}

public sealed class SwordheartForge : HbrgoSaki31OForge
{
    private const int MaximumBonusPerStack = 2;

    public int SwordheartMaximumBonus =>
        MaximumBonusPerStack * EffectiveStackCount;
}

public sealed class ChargeForge : HbrgoSaki31OForge
{
    private const string ChargeVarName = "ChargePower";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar(ChargeVarName, 1m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[]
        {
            MainModBridge.GetChargeHoverTip()
        };

    public override Task AfterEnergyResetLate(Player player)
    {
        if (Owner == null || player != Owner || Owner.Creature.IsDead)
        {
            return Task.CompletedTask;
        }

        Flash();
        return MainModBridge.ApplyCharge(
            Owner,
            DynamicVars[ChargeVarName].BaseValue * EffectiveStackCount);
    }
}

public sealed class DpForge : HbrgoSaki31OForge
{
    private const string MaxDpVarName = "MaxDp";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar(MaxDpVarName, 2m)
        };

    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (Owner == null)
        {
            return Task.CompletedTask;
        }

        int amount = DynamicVars[MaxDpVarName].IntValue *
            EffectiveStackCount;
        if (MainModBridge.GainMaxDp(Owner, amount))
        {
            Flash();
        }

        return Task.CompletedTask;
    }
}

public sealed class SixSenseForge : HbrgoSaki31OForge
{
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        if (Owner == null)
        {
            return;
        }

        if (!MainModBridge.CanOfferSixSenseReward(Owner))
        {
            return;
        }

        Flash();
        await MainModBridge.OfferSixSenseReward(Owner);
    }
}

internal static class MainModBridge
{
    private const string MainAssemblyName = "HBRGOSakiMod";
    private const string BridgeTypeName =
        "HBRGOSakiMod.Core.Integration.HbrgoSakiHextechCompatLoader";

    private static readonly Type BridgeType = ResolveBridgeType();

    public static bool Is31OPlayer(Player player)
    {
        return Invoke<bool>(nameof(Is31OPlayer), player);
    }

    public static int GetSwordheartMaximumBonus(Player player)
    {
        return Invoke<int>(nameof(GetSwordheartMaximumBonus), player?.Creature);
    }

    public static IHoverTip GetChargeHoverTip()
    {
        return Invoke<IHoverTip>(nameof(GetChargeHoverTip));
    }

    public static Task ApplyCharge(Player player, decimal amount)
    {
        return Invoke<Task>(nameof(ApplyCharge), player, amount);
    }

    public static bool GainMaxDp(Player player, int amount)
    {
        return Invoke<bool>(nameof(GainMaxDp), player, amount);
    }

    public static bool CanOfferSixSenseReward(Player player)
    {
        return Invoke<bool>(nameof(CanOfferSixSenseReward), player);
    }

    public static Task OfferSixSenseReward(Player player)
    {
        return Invoke<Task>(nameof(OfferSixSenseReward), player);
    }

    public static Task EnterDualSwords(Player player)
    {
        return Invoke<Task>(nameof(EnterDualSwords), player);
    }

    public static bool HasBowMode(Player player)
    {
        return Invoke<bool>(nameof(HasBowMode), player);
    }

    public static bool IsBowFormCard(CardModel card)
    {
        return Invoke<bool>(nameof(IsBowFormCard), card);
    }

    public static Task TransformChihayaToAnoko(Player player)
    {
        return Invoke<Task>(nameof(TransformChihayaToAnoko), player);
    }

    public static Task TransformCurseAndStatusCardsToEternalYo(
        Player player)
    {
        return Invoke<Task>(
            nameof(TransformCurseAndStatusCardsToEternalYo),
            player);
    }

    public static CardModel CreateEternalYo(Player player)
    {
        return Invoke<CardModel>(nameof(CreateEternalYo), player);
    }

    public static Task SwapMaxHpAndDp(Player player)
    {
        return Invoke<Task>(nameof(SwapMaxHpAndDp), player);
    }

    private static T Invoke<T>(string methodName, params object[] arguments)
    {
        MethodInfo method = BridgeType.GetMethod(
            methodName,
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic);
        if (method == null)
        {
            throw new MissingMethodException(BridgeTypeName, methodName);
        }

        object result = method.Invoke(null, arguments);
        if (result is T typedResult)
        {
            return typedResult;
        }

        throw new InvalidOperationException(
            $"{BridgeTypeName}.{methodName} returned an unexpected type.");
    }

    private static Type ResolveBridgeType()
    {
        Assembly mainAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(
                assembly.GetName().Name,
                MainAssemblyName,
                StringComparison.Ordinal));
        if (mainAssembly == null)
        {
            throw new InvalidOperationException(
                $"Assembly {MainAssemblyName} is not loaded.");
        }

        return mainAssembly.GetType(BridgeTypeName, throwOnError: true);
    }
}
