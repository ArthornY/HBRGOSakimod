using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class WorldConquestHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneCMemberCard
{
    internal const int Flash = 0, SuperDestroyer = 1, ThreeColorDango = 2, TrickOrTreat = 3,
        CompanionDance = 4, HelpDestroyWorld = 5, HolyAnthem = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneCMember ThirtyOneCMember => ThirtyOneCMember.BungoYayoi;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(200m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<HighDimensionalShellPower>(), HoverTipFactory.FromPower<HbrDefenseDownPower>() };
    public WorldConquestHbrgoSaki() : base(1, CardType.Attack, TargetType.AnyEnemy) { }

    public override Task BeforeCombatStart()
    { WorldConquestCombatHelper.InitializeCombat(this); return Task.CompletedTask; }
    public override Task AfterEnergyReset(Player player) => player == Owner
        ? WorldConquestCombatHelper.OnEnergyReset(player) : Task.CompletedTask;
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("BungoDpBonus", HasUpgradeEffect(SuperDestroyer) ? 100 : 30);
        text.Add("BungoServants", IsMutable && Owner != null ? ThirtyOneCKeywordRules.GetPermanentDistinctCount(Owner) : 0);
        text.Add("BungoServantsText", HasUpgradeEffect(SuperDestroyer)
            ? $"当前仆人数：{(IsMutable && Owner != null ? ThirtyOneCKeywordRules.GetPermanentDistinctCount(Owner) : 0)}。" : string.Empty);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        int roll = HasUpgradeEffect(TrickOrTreat)
            ? this.Capability<RollGlossaryCapability>()?.EnsureCurrentRollFor(this) ?? 0 : 0;
        using var action = WorldConquestCombatHelper.Begin(this, roll);
        if (HasUpgradeEffect(ThreeColorDango))
            foreach (var enemy in CombatState.HittableEnemies.ToArray())
                await PowerDispelRules.RandomRemoveBuffs(Owner, enemy, 1);

        bool validTarget = play.Target?.IsAlive == true && play.Target.Side != Owner.Creature.Side &&
            ReferenceEquals(play.Target.CombatState, CombatState);
        if (validTarget && (!HasUpgradeEffect(TrickOrTreat) || roll > 30))
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
                .Targeting(play.Target).WithHitCount(1).WithHitFx("vfx/vfx_attack_shoot").Execute(context);
        if (validTarget && action.FirstPlay && HasUpgradeEffect(HolyAnthem))
            await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, play.Target,
                20m, Owner.Creature, this, new(HbrEffectLimit.Default, HbrEffectExit.Eternal, 1));
        if (HasUpgradeEffect(CompanionDance)) await WorldConquestCombatHelper.Recruit(Owner, this);
    }
}
