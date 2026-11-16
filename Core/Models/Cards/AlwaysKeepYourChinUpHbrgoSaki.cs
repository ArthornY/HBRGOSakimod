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
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class AlwaysKeepYourChinUpHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneDMemberCard
{
    internal const int Flash = 0, SaturatedRecovery = 1, HealingSmile = 2, HeartfulExplosion = 3,
        ButterflyInvitation = 4, HeartfeltHospitality = 5, AraAra = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneDMember ThirtyOneDMember => ThirtyOneDMember.MurofushiRisa;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override TargetType TargetType => HasUpgradeEffect(SaturatedRecovery) ? TargetType.AllAllies :
        IsMutable && Owner?.Creature != null && RisaRecoveryCombatHelper.Targets(Owner).Length <= 1 ? TargetType.Self : TargetType.AnyAlly;
    internal decimal RecoveryRatio => HasUpgradeEffect(SaturatedRecovery) ? .3m : .2m;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        new HoverTip(new LocString("static_hover_tips", "HBRGO_RISA_DP.title"), new LocString("static_hover_tips", "HBRGO_RISA_DP.description")),
        HoverTipFactory.FromPower<HbrMoralePower>(), HoverTipFactory.FromPower<MindsEyePower>(),
        HoverTipFactory.FromPower<HbrAttackDownPower>()
    };
    public AlwaysKeepYourChinUpHbrgoSaki() : base(1, CardType.Skill, TargetType.AnyAlly) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("RecoveryPercent", RecoveryRatio * 100m);
        text.Add("RecoveryTarget", HasUpgradeEffect(SaturatedRecovery) ? "我方全体" : "一名友方单位");
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        Creature owner = Owner.Creature;
        Creature[] choices = RisaRecoveryCombatHelper.Targets(Owner);
        Creature[] recipients = HasUpgradeEffect(SaturatedRecovery) ? choices :
            new[] { play.Target is { } selected && choices.Contains(selected) ? selected : owner };
        foreach (Creature target in recipients)
        {
            if (HasUpgradeEffect(ButterflyInvitation) && await RisaRecoveryCombatHelper.Cleanse(context, target) > 0)
                await HbrEffectApplication.ApplyMindsEye(context, target, owner, this);
            await SeiraDpRecoveryHelper.Restore(context, target, RecoveryRatio, HasUpgradeEffect(HealingSmile), this);
        }
        if (HasUpgradeEffect(HeartfulExplosion))
            await ThirtyOneDBondHelper.GrantTeamMorale(context, owner, 4m);
        if (!HasUpgradeEffect(AraAra)) return;
        using var batch = HbrDebuffApplicationScope.BeginBatch(owner);
        foreach (Creature enemy in CombatState.GetOpponentsOf(owner).Where(enemy => enemy.IsAlive).ToArray())
            await HbrPercentDebuffApplication.ApplyBasis<HbrAttackDownPower>(context, enemy, 20m, owner, this,
                profile: new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2));
    }
}
