using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class FrozenMistMeowHbrgoSaki : ThirtyOneBGardenCard
{
    internal const int SenseHostility = 0, HalloweenFervor = 1, BurstImpulse = 2, Flash = 3,
        WindSlash = 4, HalloweenMeowNight = 5, RosePetals = 6;
    private bool _killed;
    protected override int FlashEffectId => Flash;
    public override TargetType TargetType => HasUpgradeEffect(WindSlash) ? TargetType.AllEnemies : TargetType.AnyEnemy;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(50m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<HbrMoralePower>(), HoverTipFactory.FromPower<DestructionRatePower>() };
    public FrozenMistMeowHbrgoSaki() : base(1, CardType.Attack, TargetType.AnyEnemy) { }
    internal int BaseHitCount => (HasUpgradeEffect(WindSlash) ? 3 : 4) + (HasUpgradeEffect(HalloweenMeowNight) ? 1 : 0);
    public override Task BeforeCombatStart() { ThirtyOneBPhysicalCardState.Reset(this); _killed = false; return Task.CompletedTask; }
    public override int ModifyCardPlayCount(CardModel card, Creature target, int count) => ReferenceEquals(card, this) &&
        HasUpgradeEffect(HalloweenFervor) && !ThirtyOneBPhysicalCardState.HasPlayed(this) ? count + 1 : count;
    protected override void AddExtraArgsToDescription(LocString text)
    { base.AddExtraArgsToDescription(text); text.Add("SumomoHits", BaseHitCount); text.Add("SumomoTarget", HasUpgradeEffect(WindSlash) ? "所有敌人" : "目标"); }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ThirtyOneBPhysicalCardState.MarkPlayed(this); _killed = false;
        var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .WithHitCount(BaseHitCount).WithHitFx("vfx/vfx_attack_slash");
        if (HasUpgradeEffect(WindSlash)) attack.TargetingAllOpponents(CombatState);
        else if (play.Target?.IsAlive == true) attack.Targeting(play.Target);
        else return;
        await attack.Execute(context);
        var dead = attack.Results.SelectMany(results => results).Select(result => result.Receiver)
            .Where(target => target != null && target.Side != Owner.Creature.Side && target.IsDead).Distinct().ToArray();
        _killed = dead.Length > 0;
        if (_killed && HasUpgradeEffect(RosePetals)) CardPlayCompatibility.SetResultPile(play, PileType.Hand);
        if (HasUpgradeEffect(BurstImpulse))
            foreach (var target in dead)
                await PowerCmd.Apply<HbrMoralePower>(context, Owner.Creature, 1m, Owner.Creature, this);
    }
    protected override void AfterCloned() { base.AfterCloned(); _killed = false; }
}
