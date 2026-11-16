using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ImmortalRecollectionHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneCMemberCard
{
    internal const int Flash = 0, DestructionPlus = 1, Antidote = 2, CatMagic = 3,
        CrimsonWitch = 4, NecroDance = 5, PotionRecipe = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneCMember ThirtyOneCMember => ThirtyOneCMember.AmaneMiko;
    public override TargetType TargetType => HasUpgradeEffect(DestructionPlus) ? TargetType.AllEnemies : TargetType.AnyEnemy;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HbrDefenseDownPower>(), HoverTipFactory.FromPower<FragilePower>(),
        HoverTipCompatibility.CreateKeyword(HbrCompactText.Literal("title", "药水"),
            HbrCompactText.Literal("text", "药水栏有空位时可以获得药水。药水可以在战斗中使用。"), "HBRGO_POTION_RULE")
    };
    public ImmortalRecollectionHbrgoSaki() : base(1, CardType.Skill, TargetType.AnyEnemy) { }
    public override Task BeforeCombatStart()
    { ImmortalRecollectionCombatHelper.InitializeCombat(this); return Task.CompletedTask; }
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || !HasUpgradeEffect(CatMagic) || originalCost <= 0m ||
            Owner?.Creature?.CombatState?.GetOpponentsOf(Owner.Creature).Any(enemy =>
                enemy.IsAlive && enemy.Powers.OfType<HbrDefenseDownPower>().Any(power => power.Amount > 0)) != true) return false;
        modifiedCost = Math.Max(0m, originalCost - 1m);
        return true;
    }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        decimal basis = ImmortalRecollectionCombatHelper.DefenseBasis(this);
        Creature target = HbrCardDescriptionContext.GetTarget(this);
        decimal value = target != null && IsMutable && Owner != null
            ? HbrPercentDebuffHelper.ResolveBasis(ModelDb.Power<HbrDefenseDownPower>(), basis, Owner.Creature, target) : basis;
        text.Add("MikoTarget", HasUpgradeEffect(DestructionPlus) ? "所有敌人" : "目标");
        string number = value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        text.Add("MikoDefensePercent", value > basis ? "[green]" + number + "[/green]" : value < basis ? "[red]" + number + "[/red]" : number);
        text.Add("MikoGrowth", ImmortalRecollectionCombatHelper.Growth(this));
        text.Add("MikoGrowthText", string.Empty);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool first = ImmortalRecollectionCombatHelper.MarkPlayed(this);
        decimal basis = ImmortalRecollectionCombatHelper.DefenseBasis(this);
        if (HasUpgradeEffect(Antidote)) await WorldsFinestCombatHelper.CleanseOneDebuff(this);
        Creature[] targets = HasUpgradeEffect(DestructionPlus)
            ? CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray()
            : play.Target?.IsAlive == true && play.Target.Side != Owner.Creature.Side &&
                ReferenceEquals(play.Target.CombatState, CombatState) ? new[] { play.Target } : Array.Empty<Creature>();
        using (HbrDebuffApplicationScope.BeginBatch(Owner.Creature))
            foreach (Creature target in targets)
            {
                await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, target, basis,
                    Owner.Creature, this, new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2));
                if (HasUpgradeEffect(CrimsonWitch))
                    await PowerCmd.Apply<FragilePower>(context, target, 3m, Owner.Creature, this);
            }
        if (first && HasUpgradeEffect(PotionRecipe)) await ImmortalRecollectionCombatHelper.GrantPotion(Owner);
        if (HasUpgradeEffect(NecroDance)) ImmortalRecollectionCombatHelper.Advance(this);
    }
}
