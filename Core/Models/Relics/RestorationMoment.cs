using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interactions.RightClick;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class RestorationMoment : RelicModel, IModRightClickableRelic
{
    private const string SummonLevelVar = "RedCrimsonLevel";

    private int _counter;
    private int _spentThisCombat;
    private bool _hasSummonedThisCombat;

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar(SummonLevelVar, 1m)
        };

    public override bool ShowCounter => IsMutable;

    public override int DisplayAmount => Counter;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("relics/restoration_moment.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    [SavedProperty]
    public int Counter
    {
        get => _counter;
        private set
        {
            AssertMutable();
            int nextValue = Math.Max(0, value);
            if (_counter == nextValue)
            {
                return;
            }

            _counter = nextValue;
            DynamicVars[SummonLevelVar].BaseValue =
                GetLevelForCounter(nextValue);
            InvokeDisplayAmountChanged();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasSummonedThisCombat
    {
        get => _hasSummonedThisCombat;
        private set
        {
            AssertMutable();
            _hasSummonedThisCombat = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SpentThisCombat
    {
        get => _spentThisCombat;
        private set
        {
            AssertMutable();
            _spentThisCombat = Math.Max(0, value);
        }
    }

    public bool CanSummon =>
        IsMutable &&
        !HasSummonedThisCombat &&
        CombatManager.Instance.IsInProgress &&
        !CombatManager.Instance.IsOverOrEnding &&
        !CombatManager.Instance.PlayerActionsDisabled &&
        CombatManager.Instance.PlayersTakingExtraTurn.Count == 0 &&
        Owner.Creature.IsAlive &&
        Owner.Creature.CombatState?.CurrentSide == CombatSide.Player &&
        !Owner.Creature.CombatState.Allies.Any(
            creature =>
                creature.IsAlive &&
                creature.PetOwner == Owner &&
                creature.Monster is RedCrimson);

    public bool CanHandleRightClickLocal(ModRightClickContext context) =>
        CanSummon;

    public bool CanExecuteRightClick(ModRightClickExecutionContext context) =>
        CanSummon;

    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (TryBeginSummon(out Task summonTask))
        {
            await summonTask;
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Counter += room.RoomType switch
        {
            RoomType.Boss => 20,
            RoomType.Elite => 10,
            _ => 5
        };
        SpentThisCombat = 0;
        HasSummonedThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        SpentThisCombat = 0;
        return Task.CompletedTask;
    }

    public bool TryBeginSummon(out Task summonTask)
    {
        if (!CanSummon)
        {
            summonTask = Task.CompletedTask;
            return false;
        }

        HasSummonedThisCombat = true;
        Status = RelicStatus.Active;
        Flash();
        summonTask = SummonInternal();
        return true;
    }

    internal void SpendCounter(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        int oldCounter = Counter;
        Counter -= amount;
        int spent = oldCounter - Counter;
        if (spent > 0)
        {
            SpentThisCombat += spent;
            Flash();
        }
    }

    internal void GainCounter(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Counter += amount;
        Flash();
    }

    internal static int GetLevelForCounter(int counter)
    {
        return counter switch
        {
            >= 160 => 6,
            >= 80 => 5,
            >= 55 => 4,
            >= 30 => 3,
            >= 15 => 2,
            _ => 1
        };
    }

    private async Task SummonInternal()
    {
        try
        {
            ICombatState combatState = Owner.Creature.CombatState ??
                throw new InvalidOperationException(
                    "Cannot summon Red Crimson outside combat.");
            RedCrimson redCrimson =
                (RedCrimson)ModelDb.Monster<RedCrimson>().ToMutable();
            redCrimson.Configure(
                GetLevelForCounter(Counter),
                Owner.Creature.HasPower<PowerReleasePower>() ? 1 : 0);

            Creature creature = combatState.CreateCreature(
                redCrimson,
                Owner.Creature.Side,
                null);
            await PlayerCmd.AddPet(creature, Owner);
            await HighDimensionalShellCombatStartPatch.GrantShell(
                creature,
                replaceExisting: false);

            creature.PrepareForNextTurn(
                combatState
                    .GetOpponentsOf(creature)
                    .Where(target => target.IsHittable));
            NCombatRoom.Instance?.SetCreatureIsInteractable(
                creature,
                on: true);
        }
        catch
        {
            HasSummonedThisCombat = false;
            Status = RelicStatus.Normal;
            throw;
        }
    }
}
