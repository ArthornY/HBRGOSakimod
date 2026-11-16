using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Content;
using STS2RitsuLib.Keywords;

namespace HBRGOSakiMod.Core.Models.Powers;

/// <summary>Battle-long upgrades enabled by playing It's MYGO; dormant without its shield.</summary>
public sealed class ItsMygoProtectionPower : PowerModel, ICustomPower
{
    private static readonly AsyncLocal<int> ReactionDepth = new();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool EnhancedShield { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool RetaliationEnabled { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool RecoveryEnabled { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool NotOverYetEnabled { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HitsUsedThisTurn { get; private set; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override Color AmountLabelColor => new("F2A5D0");
    public string CustomPackedIconPath => "res://images/atlases/power_atlas.sprites/strongest_shield_power.tres";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            if (!IsMutable) return base.Description;
            LocString text = new("powers", "ITS_MYGO_PROTECTION_POWER.activeDescription");
            List<string> effects = new();
            if (EnhancedShield) effects.Add(EffectText(3));
            if (RetaliationEnabled) effects.Add(EffectText(4));
            if (RecoveryEnabled && RetaliationEnabled) effects.Add(EffectText(5));
            if (NotOverYetEnabled) effects.Add(EffectText(6));
            text.Add("Effects", string.Join("\n", effects));
            return text;
        }
    }

    private static string EffectText(int id) =>
        new LocString("cards", "ANON_AID_HBRGO_SAKI.upgrade_effect." + id).GetFormattedText();

    internal static async Task Enable(PlayerChoiceContext context, Player player, CardModel source,
        bool enhancedShield, bool retaliation, bool recovery, bool notOverYet)
    {
        if (!(enhancedShield || retaliation || notOverYet)) return;
        ItsMygoProtectionPower power = player.Creature.GetPower<ItsMygoProtectionPower>() ??
            await PowerCmd.Apply<ItsMygoProtectionPower>(context, player.Creature, 1m, player.Creature, source);
        if (power == null) return;
        power.AssertMutable();
        // Replaying or playing another upgraded copy unions its effects, never doubles them.
        power.EnhancedShield |= enhancedShield;
        power.RetaliationEnabled |= retaliation;
        power.RecoveryEnabled |= recovery && power.RetaliationEnabled;
        power.NotOverYetEnabled |= notOverYet;
        player.Creature.InvokePowerModified(power, 0, true);
    }

    internal static StrongestShieldPower GetActiveShield(Creature creature) =>
        creature?.Powers.OfType<StrongestShieldPower>()
            .FirstOrDefault(power => power.GetType() == typeof(StrongestShieldPower) && power.Amount > 0);

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner)) HitsUsedThisTurn = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (target != Owner || Amount <= 0 || ReactionDepth.Value != 0 ||
            Owner.Player == null || !Owner.IsAlive || Owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding || dealer == null || dealer.Side == Owner.Side ||
            !ItsMygoIncomingDamageCapture.TryGetRaw(target, result, out decimal rawDamage) || rawDamage <= 0m)
            return;

        StrongestShieldPower shield = GetActiveShield(Owner);
        if (shield == null) return;
        NotOverYetPower notOverYet = Owner.GetPower<NotOverYetPower>();
        bool triggerChain = false;
        if (NotOverYetEnabled && notOverYet?.Amount > 0 && props.IsPoweredAttack())
        {
            triggerChain = HitsUsedThisTurn < shield.Amount;
            if (HitsUsedThisTurn < int.MaxValue) HitsUsedThisTurn++;
        }

        if (!(RetaliationEnabled && dealer.IsHittable) && !triggerChain) return;
        ReactionDepth.Value++;
        try
        {
            Flash();
            if (RetaliationEnabled && dealer.IsHittable)
            {
                // The reflected amount is a base damage value: enemy Block still applies.
                using var reflectedSkill = HbrBattleStatsHelper.IsEnabled(Owner)
                    ? HbrEffectSkillDamageContext.BeginWorld(dealer, Owner, null, rawDamage, ValueProp.Unpowered) : null;
                IEnumerable<DamageResult> reflected = await CreatureCommandCompatibility.Damage(
                    choiceContext, dealer, rawDamage, ValueProp.Unpowered, Owner, null);
                if (RecoveryEnabled && Owner.IsAlive && GetActiveShield(Owner) != null)
                {
                    long dealt = reflected.Where(hit => hit.Receiver == dealer)
                        .Sum(hit => (long)Math.Max(0, hit.UnblockedDamage));
                    int recovery = (int)Math.Min(int.MaxValue, dealt / 4);
                    if (recovery > 0)
                    {
                        int restored = Owner.Player.GetRelic<DeflectorProtection>()?.RestoreDp(recovery) ?? 0;
                        MariaRepentanceCombatHelper.OnDpRestored(Owner, restored, null, fromAbility: true);
                    }
                }
            }

            if (triggerChain && Owner.IsAlive && GetActiveShield(Owner) != null &&
                Owner.GetPower<NotOverYetPower>() == notOverYet && !CombatManager.Instance.IsOverOrEnding)
            {
                // A fresh owned source keeps extra-roll state separate from the already played power card.
                // It is never played or added to any pile, and is removed even if the chain fails.
                AnonAidHbrgoSaki source = Owner.CombatState.CreateCard<AnonAidHbrgoSaki>(Owner.Player);
                try
                {
                    // Pileless cards do not receive combat-only keyword hooks. Add
                    // the real registered keyword explicitly, still respecting Fate.
                    if (RollKeywordRules.IsRollEnabled(source) &&
                        ModKeywordRegistry.TryGetCardKeyword(
                            ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "ROLL"),
                            out CardKeyword rollKeyword))
                        source.AddKeyword(rollKeyword);
                    await notOverYet.TriggerAdditionalChains(choiceContext, source, dealer, 1);
                }
                finally { if (!source.HasBeenRemovedFromState) source.RemoveFromState(); }
            }
        }
        finally { ReactionDepth.Value--; }
    }
}
