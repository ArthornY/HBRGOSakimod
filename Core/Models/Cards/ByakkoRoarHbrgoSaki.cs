using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ByakkoRoarHbrgoSaki : CardModel
{
    internal const int Flash = 0, Innocence = 1, BeastCage = 2, RageClaw = 3,
        DestructionQueen = 4, SunnyRelaxation = 5, FiftyKilometersPerHour = 6;
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasAutoPlayedThisCombat { get; private set; }

    public override int MaxUpgradeLevel => UpgradeEffectCount;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => HbrMemoryGardenPortraits.For(this);
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { HbrSkillVars.Damage(250m, ValueProp.Move, divisor: 3m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HbrDefenseDownPower>(),
        HoverTipFactory.FromPower<HbrAttackUpPower>(),
        HoverTipFactory.FromPower<HbrMoralePower>(),
        HoverTipFactory.FromPower<DestructionRatePower>()
    };

    public ByakkoRoarHbrgoSaki() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    internal bool HasUpgradeEffect(int id) => UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(id);
    internal IReadOnlyList<int> GetAvailableUpgradeEffects() => Enumerable.Range(0, UpgradeEffectCount)
        .Where(id => !HasUpgradeEffect(id)).ToArray();
    internal bool PrepareUpgradeEffect(int id)
    {
        if (!IsMutable || !IsUpgradable || !GetAvailableUpgradeEffects().Contains(id)) return false;
        _preparedUpgradeEffect = id;
        return true;
    }
    internal void CancelPreparedUpgradeEffect() => _preparedUpgradeEffect = null;
    internal static int CalculateHitCount(bool cage, bool rage, int livingEnemies) =>
        2 + (cage ? 4 : 0) + (rage && livingEnemies == 1 ? 3 : 0);
    internal int HitCount => CalculateHitCount(HasUpgradeEffect(BeastCage), HasUpgradeEffect(RageClaw),
        IsMutable && Owner?.Creature.CombatState is { } combat ? combat.Enemies.Count(enemy => enemy.IsAlive) : 0);

    public override Task BeforeCombatStart()
    {
        HasAutoPlayedThisCombat = false;
        return Task.CompletedTask;
    }
    public override async Task AfterAutoPrePlayPhaseEnteredEarly(PlayerChoiceContext context, Player player)
    {
        if (player != Owner || !HasUpgradeEffect(FiftyKilometersPerHour) || HasAutoPlayedThisCombat ||
            player.PlayerCombatState?.TurnNumber != 1 ||
            Pile?.Type is not (PileType.Draw or PileType.Hand or PileType.Discard)) return;
        HasAutoPlayedThisCombat = true;
        await CardCmd.AutoPlay(context, this, null);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("ByakkoHits", HitCount);
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel).Where(id => id != Flash)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString())).ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
        decimal basis = DynamicVars.Damage.BaseValue;
        decimal bonus = IsMutable && Owner != null
            ? HBRGOSakiMod.Core.Models.Relics.TigersMouthSnackBond.GetDamageBonusUnits(Owner.Creature, this) : 0m;
        HbrSkillCardDescription.AddDamageParameters(this, description, "Damage", basis + bonus, previewUnits: basis);
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        ReferenceEquals(cardSource, this) && dealer == Owner?.Creature && props.IsPoweredAttack() &&
            HasUpgradeEffect(Innocence) ? 1.5m : 1m;

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (HasUpgradeEffect(SunnyRelaxation))
            await PowerCmd.Apply<HbrMoralePower>(context, Owner.Creature, 2m, Owner.Creature, this);
        if (HasUpgradeEffect(Innocence))
            await ApplySelfDefenseDown(context);
        if (HasUpgradeEffect(BeastCage))
            await HbrEffectApplication.Apply<HbrAttackUpPower>(context, Owner.Creature, 50m,
                Owner.Creature, this, remaining: 5,
                profile: new HbrEffectProfile(HbrEffectLimit.Only, HbrEffectExit.PlayerTurnEnd, 5));
        if (HasUpgradeEffect(DestructionQueen))
        {
            using var batch = HbrDebuffApplicationScope.BeginBatch(Owner.Creature);
            foreach (Creature enemy in CombatState.HittableEnemies.ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy,
                    20m, Owner.Creature, this,
                    profile: new HbrEffectProfile(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2));
        }
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .TargetingAllOpponents(CombatState).WithHitCount(HitCount)
            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if (HasUpgradeEffect(BeastCage) && Owner.Creature.IsAlive)
            await ApplySelfDefenseDown(context);
    }

    private Task ApplySelfDefenseDown(PlayerChoiceContext context) =>
        HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, Owner.Creature,
            50m, Owner.Creature, this,
            profile: new HbrEffectProfile(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));

    protected override void OnUpgrade()
    {
        int index = CurrentUpgradeLevel - 1;
        if (index < 0 || index >= UpgradeEffectCount) return;
        int[] applied = UpgradeEffectOrder.Take(index).ToArray();
        bool Eligible(int id) => id is >= 0 and < UpgradeEffectCount && !applied.Contains(id);
        int selected = index < UpgradeEffectOrder.Length && Eligible(UpgradeEffectOrder[index])
            ? UpgradeEffectOrder[index]
            : _preparedUpgradeEffect is int prepared && Eligible(prepared)
                ? prepared : Enumerable.Range(0, UpgradeEffectCount).First(Eligible);
        if (index < UpgradeEffectOrder.Length) UpgradeEffectOrder[index] = selected;
        else UpgradeEffectOrder = UpgradeEffectOrder.Append(selected).ToArray();
        _preparedUpgradeEffect = null;
        if (selected == Flash) EnergyCost.UpgradeBy(-1);
    }
    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();
        UpgradeEffectOrder = (UpgradeEffectOrder ?? Array.Empty<int>()).Where(id => id is >= 0 and < UpgradeEffectCount)
            .Distinct().Take(UpgradeEffectCount).ToArray();
        _preparedUpgradeEffect = null;
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        UpgradeEffectOrder = UpgradeEffectOrder.ToArray();
        _preparedUpgradeEffect = null;
    }
    protected override void AfterDowngraded()
    {
        base.AfterDowngraded();
        UpgradeEffectOrder = Array.Empty<int>();
        _preparedUpgradeEffect = null;
    }
}
