using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

public abstract class HotaruCannonModel : MonsterModel
{
    public abstract HotaruCannonKind Kind { get; }
    protected abstract string SceneName { get; }

    // Creature targeting requires an alive engine entity. This hidden sentinel
    // has no gameplay HP, receives no damage, and never intercepts player hits.
    public override int MinInitialHp => 1;
    public override int MaxInitialHp => 1;
    // v0.113 moved this visibility rule to MonsterModel.IsInteractable.
    // The runtime bridge handles both native getters without a removed override.
    public bool IsHealthBarVisible => false;
    public override bool HasDeathSfx => false;
    public override bool ShouldFadeAfterDeath => false;
    public override bool ShouldShowInCompendium => false;
    protected override string VisualsPath => SceneHelper.GetScenePath("creature_visuals/hotaru_cannon_" + SceneName);

    // The native virtual hook gained CardPlay on beta. Keep this entry point
    // nonvirtual and let the shared damage bridge call the same immunity rule.
    public decimal ModifyDamageCap(Creature target, ValueProp props, Creature dealer, CardModel cardSource) =>
        GetCannonDamageCap(target);

    internal decimal GetCannonDamageCap(Creature target) =>
        IsMutable && target == Creature ? 0m : decimal.MaxValue;

    public override bool ShouldDie(Creature creature) => !IsMutable || creature != Creature;

    protected Millsage Relic => IsMutable ? Creature.PetOwner?.GetRelic<Millsage>() : null;
    private HotaruCannonState State => Relic?.StateFor(Kind) ?? new HotaruCannonState();
    // Charge repeats whole actions; individual base values are never doubled.
    internal int PhaseMultiplier(HotaruCannonPhase phase) => 1;
    private int EndHitCount => HotaruCannonVigorHelper.GetPlannedHitCount(Relic, Kind, HotaruCannonPhase.End);
    internal virtual decimal CurrentDamage => Kind switch
    {
        HotaruCannonKind.Happiness => State.HappinessDamage,
        HotaruCannonKind.Contest => HotaruCannonSkillPowerHelper.ContestEndDamage * PhaseMultiplier(HotaruCannonPhase.End),
        HotaruCannonKind.Longing => HotaruCannonSkillPowerHelper.LongingEndDamage * PhaseMultiplier(HotaruCannonPhase.End),
        HotaruCannonKind.Absolution => HotaruCannonSkillPowerHelper.AbsolutionEndDamage * PhaseMultiplier(HotaruCannonPhase.End),
        _ => 0m
    };

    internal virtual LocString IntentDescription()
    {
        LocString text = HotaruCannonSkillPowerHelper.Description("monsters", Id.Entry + ".intentDescription", Relic?.Owner);
        string template = text.GetRawText();
        Creature cannon = IsMutable ? Creature : null;
        HotaruCannonSkillPowerHelper.AddDescriptionVariables(text, Kind,
            damage: HotaruCannonDamageUpgradeHelper.PrepareBaseDamage(cannon, null, CurrentDamage),
            growth: State.NextHappinessIncrease * PhaseMultiplier(HotaruCannonPhase.End),
            block: HotaruCannonSkillPowerHelper.StartBlock(Kind) + HotaruUncommonCannonHelper.BlockBonus(cannon),
            startDamage: HotaruCannonDamageUpgradeHelper.PrepareBaseDamage(cannon, null, HotaruCannonSkillPowerHelper.AbsolutionStartDamage),
            endDamage: HotaruCannonDamageUpgradeHelper.PrepareBaseDamage(cannon, null, HotaruCannonSkillPowerHelper.AbsolutionEndDamage));
        // A single template describes only this cannon's actions. Avoid full
        // damage hooks for unused fields while supporting translated templates.
        if (template.Contains("{Damage", StringComparison.Ordinal))
            text.Add("Damage", HotaruCannonStatsHelper.FormatDamagePreview(cannon, CurrentDamage, includeContestBonus: true));
        text.Add("Growth", Math.Min(HotaruCannonState.MaximumStoredValue,
            State.NextHappinessIncrease * PhaseMultiplier(HotaruCannonPhase.End)).ToString(CultureInfo.InvariantCulture));
        if (template.Contains("{Block", StringComparison.Ordinal))
            text.Add("Block", CardDamageNumberFormatter.Format(HotaruCannonBlockHelper.Preview(
                cannon?.PetOwner?.Creature, HotaruCannonSkillPowerHelper.StartBlock(Kind) * PhaseMultiplier(HotaruCannonPhase.Start), null, cannon)));
        if (template.Contains("{StartDamage", StringComparison.Ordinal))
            text.Add("StartDamage", HotaruCannonStatsHelper.FormatDamagePreview(cannon, HotaruCannonSkillPowerHelper.AbsolutionStartDamage * PhaseMultiplier(HotaruCannonPhase.Start), previewPhase: HotaruCannonPhase.Start));
        text.Add("Repeat", HotaruCannonVigorHelper.GetPlannedHitCount(Relic, Kind, HotaruCannonPhase.Start));
        if (template.Contains("{EndDamage", StringComparison.Ordinal))
            text.Add("EndDamage", HotaruCannonStatsHelper.FormatDamagePreview(cannon, HotaruCannonSkillPowerHelper.AbsolutionEndDamage * PhaseMultiplier(HotaruCannonPhase.End)));
        // Contest's extra damage per debuff kind on the target (per lost six-sense
        // resistance before 2026-11-09); the variable keeps its name.
        text.Add("ResistanceBonus", PhaseMultiplier(HotaruCannonPhase.End));
        text.Add("EndRepeat", EndHitCount);
        string upgrades = Relic == null ? string.Empty : HotaruCannonUpgrades.ObtainedEffects(Relic, Kind);
        text.Add("Upgrades", string.IsNullOrEmpty(upgrades) ? string.Empty : "\n\n[gold]已解锁升级[/gold]\n" + upgrades);
        text.Add("Charged", State.PendingActionRepeats > 0
            ? "\n[gold]充能：下一次完整行动额外触发" + State.PendingActionRepeats +
                "次。下一次实际触发时消耗；活力只作用于首轮完整行动。[/gold]"
            : string.Empty);
        return text;
    }

    // Entity hover uses the full cannon ability, independently of which intent
    // icons are currently visible. Build it anew so growth and charges stay live.
    internal HoverTip CreateAbilityHoverTip() => new(Title, IntentDescription());

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        List<AbstractIntent> intents = new();
        if (Kind == HotaruCannonKind.Absolution)
            intents.Add(new CannonMultiAttackIntent(() => HotaruCannonSkillPowerHelper.AbsolutionStartDamage * PhaseMultiplier(HotaruCannonPhase.Start), 3, IntentDescription));
        if (Kind != HotaruCannonKind.Coffin)
            intents.Add(new CannonSingleAttackIntent(() => CurrentDamage, IntentDescription, () => EndHitCount));
        if (Kind is HotaruCannonKind.Coffin or HotaruCannonKind.Longing)
            intents.Add(new CannonDefendIntent(() => HotaruCannonSkillPowerHelper.StartBlock(Kind) * PhaseMultiplier(HotaruCannonPhase.Start), IntentDescription));
        // Natural phases belong to Millsage. Native pet/monster turns do nothing.
        MoveState idle = new("HOTARU_CANNON_IDLE", _ => Task.CompletedTask, intents.ToArray());
        return ModMonsterMoveStateMachines.SingleMoveLoop(idle);
    }

    private sealed class CannonSingleAttackIntent : SingleAttackIntent
    {
        private readonly Func<decimal> _damage;
        private readonly Func<LocString> _description;
        private readonly Func<int> _repeats;
        internal CannonSingleAttackIntent(Func<decimal> damage, Func<LocString> description, Func<int> repeats) : base(damage)
        { _damage = damage; _description = description; _repeats = repeats; }
        // Use real enemy candidates and the same final owner modifiers as
        // firing; never apply the local player's defenses.
        public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner) =>
            BigCombatNumbers.ToNativeSigned(DamageAmplificationLimits.MultiplySaturated(
                HotaruCannonStatsHelper.PreviewDamage(owner, _damage(), includeContestBonus: true), _repeats()));
        public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
        {
            int repeats = _repeats();
            LocString text = new("intents", repeats > 1 ? "FORMAT_DAMAGE_MULTI" : "FORMAT_DAMAGE_SINGLE");
            text.Add("Damage", HotaruCannonStatsHelper.FormatDamagePreview(owner, _damage(), includeContestBonus: true));
            if (repeats > 1) text.Add("Repeat", repeats);
            return text;
        }
        protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner) => _description();
    }

    private sealed class CannonMultiAttackIntent : MultiAttackIntent
    {
        private readonly Func<decimal> _damage;
        private readonly Func<LocString> _description;
        internal CannonMultiAttackIntent(Func<decimal> damage, int repeat, Func<LocString> description) : base(0, repeat)
        { _damage = damage; _description = description; DamageCalc = damage; }
        private int CurrentRepeats(Creature owner) => owner?.Monster is HotaruCannonModel model
            ? HotaruCannonVigorHelper.GetPlannedHitCount(owner.PetOwner?.GetRelic<Millsage>(), model.Kind, HotaruCannonPhase.Start)
            : Repeats;
        public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner) =>
            BigCombatNumbers.ToNativeSigned(DamageAmplificationLimits.MultiplySaturated(
                HotaruCannonStatsHelper.PreviewDamage(owner, _damage(), previewPhase: HotaruCannonPhase.Start), CurrentRepeats(owner)));
        public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
        {
            LocString text = new("intents", "FORMAT_DAMAGE_MULTI");
            text.Add("Damage", HotaruCannonStatsHelper.FormatDamagePreview(owner, _damage(), previewPhase: HotaruCannonPhase.Start));
            text.Add("Repeat", CurrentRepeats(owner));
            return text;
        }
        protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner) => _description();
    }

    internal sealed class CannonDefendIntent : DefendIntent
    {
        private readonly Func<decimal> _block;
        private readonly Func<LocString> _description;
        internal CannonDefendIntent(Func<decimal> block, Func<LocString> description)
        { _block = block; _description = description; }
        public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
        {
            LocString text = new("intents", "FORMAT_DAMAGE_SINGLE");
            text.Add("Damage", CardDamageNumberFormatter.Format(
                HotaruCannonBlockHelper.Preview(owner?.PetOwner?.Creature, _block(), null, owner)));
            return text;
        }
        protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner) => _description();
    }
}

public sealed class HotaruHappinessCannon : HotaruCannonModel
{
    public override HotaruCannonKind Kind => HotaruCannonKind.Happiness;
    protected override string SceneName => "happiness";
}
public sealed class HotaruCoffinCannon : HotaruCannonModel
{
    public override HotaruCannonKind Kind => HotaruCannonKind.Coffin;
    protected override string SceneName => "coffin";
}
public sealed class HotaruContestCannon : HotaruCannonModel
{
    public override HotaruCannonKind Kind => HotaruCannonKind.Contest;
    protected override string SceneName => "contest";
}
public sealed class HotaruLongingCannon : HotaruCannonModel
{
    public override HotaruCannonKind Kind => HotaruCannonKind.Longing;
    protected override string SceneName => "longing";
}
public sealed class HotaruAbsolutionCannon : HotaruCannonModel
{
    public override HotaruCannonKind Kind => HotaruCannonKind.Absolution;
    protected override string SceneName => "absolution";
}
