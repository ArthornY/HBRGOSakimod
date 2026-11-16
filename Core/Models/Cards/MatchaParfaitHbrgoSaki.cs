using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MatchaParfaitHbrgoSaki : CardModel
{
    internal const int InterestingWoman = 0, WhiteGun = 1, WildCat = 2,
        PawPrint = 3, Improvisation = 4, BlackGun = 5, ILoveIt = 6;
    private const int UpgradeEffectCount = 7;
    private int? _preparedUpgradeEffect;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] UpgradeEffectOrder { get; private set; } = Array.Empty<int>();
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool WhiteGunUsedThisCombat { get; private set; }
    [SavedProperty]
    public int ImprovisationEffect { get; private set; } = -1;

    public override int MaxUpgradeLevel => UpgradeEffectCount;
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/matcha_parfait_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    public override bool CanBeGeneratedByModifiers => false;
    internal bool UsesThirtyAEffects => IsMutable && Owner != null
        ? Owner.Character is HbrgoSaki30A
        : HbrgoSakiCardPortraitContext.LibraryVariant == HbrgoSakiVariant.ThirtyA;
    internal int HitCount => HasUpgradeEffect(BlackGun) ? 4 : 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { HbrSkillVars.Damage(100m, ValueProp.Move) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<FragilePower>(),
        HoverTipFactory.FromPower<HighDimensionalShellPower>(),
        HoverTipFactory.FromPower<DestructionRatePower>()
    };

    public MatchaParfaitHbrgoSaki() : base(1, CardType.Attack, CardRarity.Rare, TargetType.RandomEnemy) { }

    internal IReadOnlyList<int> GetAvailableUpgradeEffects() =>
        Enumerable.Range(0, UpgradeEffectCount).Where(id => !HasUpgradeEffect(id)).ToArray();
    internal bool HasUpgradeEffect(int id) => UpgradeEffectOrder.Take(CurrentUpgradeLevel).Contains(id);
    internal bool PrepareUpgradeEffect(int id)
    {
        if (!IsMutable || !IsUpgradable || !GetAvailableUpgradeEffects().Contains(id)) return false;
        _preparedUpgradeEffect = id;
        return true;
    }
    internal void CancelPreparedUpgradeEffect() => _preparedUpgradeEffect = null;

    public override Task BeforeCombatStart()
    {
        WhiteGunUsedThisCombat = false;
        ImprovisationEffect = -1;
        if (HasUpgradeEffect(Improvisation))
            ImprovisationEffect = Owner.RunState.Rng.Niche.NextInt(3);
        RanaByakkoCombatHelper.InitializeMatchaBattle(this);
        return Task.CompletedTask;
    }

    internal void SetImprovisation(int effect) => ImprovisationEffect = effect;
    internal void MarkWhiteGunUsed() => WhiteGunUsedThisCombat = true;
    internal void SetBattleState(bool whiteGunUsed, int improvisation)
    {
        WhiteGunUsedThisCombat = whiteGunUsed;
        ImprovisationEffect = improvisation;
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("MatchaHits", HitCount);
        string[] effects = UpgradeEffectOrder.Take(CurrentUpgradeLevel)
            .Where(id => id != BlackGun)
            .Select(id => HbrMemoryGardenUpgradeCatalog.GetEffectDescription(this, id.ToString())).ToArray();
        description.Add("UpgradeEffects", effects.Length == 0 ? string.Empty : "\n" + string.Join("\n", effects));
        string improvisation = HasUpgradeEffect(Improvisation) && ImprovisationEffect is >= 0 and <= 2
            ? "\n" + new LocString("cards", Id.Entry + ".improvisation." + ImprovisationEffect).GetFormattedText()
            : string.Empty;
        description.Add("ImprovisationEffect", improvisation);
        decimal basis = DynamicVars.Damage.BaseValue;
        decimal bonus = IsMutable && Owner != null
            ? HBRGOSakiMod.Core.Models.Relics.TigersMouthSnackBond.GetDamageBonusUnits(Owner.Creature, this) : 0m;
        HbrSkillCardDescription.AddDamageParameters(this, description, "Damage", basis + bonus, previewUnits: basis);
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (!ReferenceEquals(cardSource, this) || dealer != Owner?.Creature || !props.IsPoweredAttack()) return 1m;
        decimal multiplier = 1m;
        if (HasUpgradeEffect(Improvisation))
        {
            if (ImprovisationEffect == 0) multiplier *= 1.3m;
            if (ImprovisationEffect == 1 && RanaByakkoCombatHelper.HasDp(target)) multiplier *= 1.5m;
        }
        if (HasUpgradeEffect(ILoveIt) && target?.Powers.Any(power => power.Type == PowerType.Buff && power.Amount > 0) == true)
            multiplier *= 1.5m;
        return multiplier;
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        RanaByakkoCombatHelper.SynchronizeMatchaBattleState(this);
        using (RanaByakkoCombatHelper.BeginPawPrint(this))
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
                .TargetingRandomOpponents(CombatState).WithHitCount(HitCount)
                .WithHitFx("vfx/vfx_attack_slash").Execute(context);
        }
        if (HasUpgradeEffect(WhiteGun) && RanaByakkoCombatHelper.TryUseWhiteGun(this))
            await HbrMatchaDpHelper.Restore(context, Owner, 0.2m, false, this);
    }

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
