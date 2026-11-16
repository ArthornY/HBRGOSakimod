using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class WorldIsMyLaboratoryHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneCMemberCard
{
    internal const int Flash = 0, WillScythe = 1, MadScientist = 2, AbsoluteZero = 3,
        FearOfDevil = 4, DevilReturns = 5, BigBang = 6;
    public ThirtyOneCMember ThirtyOneCMember => ThirtyOneCMember.YamawakiVonIvar;
    protected override int FlashEffectId => Flash;
    internal int MemberCount => IsMutable && Owner != null ? ThirtyOneCKeywordRules.GetPermanentDistinctCount(Owner) : 0;
    internal bool IsBigBang => HasUpgradeEffect(BigBang) && MemberCount >= 6;
    internal int BaseHitCount => IsBigBang ? 10 : 4;
    internal int HpBonusPercent => HasUpgradeEffect(AbsoluteZero) && MemberCount >= 3 ? 50 : 30;
    public override TargetType TargetType => IsBigBang ? TargetType.AllEnemies : TargetType.AnyEnemy;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(50m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<BreakPower>(), HoverTipFactory.FromPower<DestructionRatePower>(),
      HoverTipFactory.FromPower<FearOfDevilPower>() };
    public WorldIsMyLaboratoryHbrgoSaki() : base(1, CardType.Attack, TargetType.AnyEnemy) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("YamawakiHits", BaseHitCount);
        text.Add("YamawakiTarget", IsBigBang ? "所有敌人" : "目标");
        text.Add("YamawakiHpBonus", HpBonusPercent);
        text.Add("YamawakiMembers", MemberCount);
        text.Add("YamawakiCount", string.Empty);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        using var receipt = YamawakiLaboratoryCombatHelper.BeginPlay(this);
        if (HasUpgradeEffect(FearOfDevil))
            foreach (var enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
                await FearOfDevilPower.Apply(context, enemy, Owner.Creature, MemberCount >= 6);
        var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .WithHitCount(BaseHitCount).WithHitFx("vfx/vfx_attack_slash");
        if (IsBigBang) attack.TargetingAllOpponents(CombatState);
        else if (play.Target?.IsAlive == true) attack.Targeting(play.Target);
        else return;
        await attack.Execute(context);
    }
}
