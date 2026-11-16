using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class FamilyIsLikeThisBond : TeamCollectionRelic, IActiveDuoBond
{
    private sealed class TurnUse { internal bool Used; }
    private ConditionalWeakTable<object, TurnUse> _extraTurnUses = new();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool PlayerTurnUsed { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool EnemyTurnUsed { get; private set; }

    public bool IsBondActive => IsActive;
    internal bool IsActive => !IsMelted && Owner != null && DisplayAmount >= 2 && HbrBattleStatsHelper.IsEnabled(Owner);
    public override string PackedIconPath => ImageHelper.GetImagePath("relics/crychic.png");
    protected override int CountCards(Player player) => ThirtyOneBBondHelper.SisterKinds(player);

    private bool WasUsed(object extraTurn, bool enemyTurn) => extraTurn != null
        ? _extraTurnUses.TryGetValue(extraTurn, out TurnUse use) && use.Used
        : enemyTurn ? EnemyTurnUsed : PlayerTurnUsed;
    private void SetUsed(object extraTurn, bool enemyTurn, bool used)
    {
        if (extraTurn != null) _extraTurnUses.GetOrCreateValue(extraTurn).Used = used;
        else if (enemyTurn) EnemyTurnUsed = used;
        else PlayerTurnUsed = used;
    }
    internal bool UsedThisTurn => IsMutable && Owner?.Creature?.CombatState is { } combat &&
        WasUsed(OverclockImmediateTurnCoordinator.GetTurnToken(Owner), combat.CurrentSide == CombatSide.Enemy);

    public override Task BeforeCombatStart()
    {
        PlayerTurnUsed = EnemyTurnUsed = false;
        _extraTurnUses = new();
        return Task.CompletedTask;
    }
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player == Owner && OverclockImmediateTurnCoordinator.GetTurnToken(Owner) == null)
            PlayerTurnUsed = false;
        return Task.CompletedTask;
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Enemy) EnemyTurnUsed = false;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play)
    {
        CardModel played = play?.Card;
        if (!IsActive || played?.Owner != Owner || Owner.PlayerCombatState == null ||
            Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding ||
            played is not (HellfireBurnHbrgoSaki or FrozenMistMeowHbrgoSaki)) return;
        object extra = OverclockImmediateTurnCoordinator.GetTurnToken(Owner);
        bool enemyTurn = Owner.Creature.CombatState.CurrentSide == CombatSide.Enemy;
        if (WasUsed(extra, enemyTurn)) return;
        bool IsOther(CardModel card) => card.Owner == Owner && !card.HasBeenRemovedFromState &&
            (played is HellfireBurnHbrgoSaki ? card is FrozenMistMeowHbrgoSaki : card is HellfireBurnHbrgoSaki);
        if (PileType.Hand.GetPile(Owner).Cards.Any(IsOther)) return;
        CardModel target = PileType.Draw.GetPile(Owner).Cards.FirstOrDefault(IsOther) ??
            PileType.Discard.GetPile(Owner).Cards.FirstOrDefault(IsOther);
        if (target == null) return;
        // Reserve before awaiting the real pile command. Its hooks may trigger
        // another replay; a rejected transfer releases this same turn's use.
        SetUsed(extra, enemyTurn, true);
        CardPileAddResult result = await CardPileCmd.Add(target, PileType.Hand);
        if (!result.success) SetUsed(extra, enemyTurn, false);
        else Flash();
    }

    internal void AddDescriptionVariables(LocString text)
    {
        text.Add("ActiveText", IsMutable && IsActive ? "已生效" : "未生效");
        text.Add("UsedText", IsMutable && UsedThisTurn ? "本回合已使用" : "本回合未使用");
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _extraTurnUses = new();
    }
}
