using System;
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
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class OpenTheLockHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneEMemberCard
{
    internal const int Flash=0, AngryRift=1, Mirage=2, HellfireGate=3,
        EscapeArtist=4, Mischief=5, TreasureUnlock=6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneEMember ThirtyOneEMember => ThirtyOneEMember.OshimaIsuzu;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(51m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<DestructionRatePower>(),HoverTipFactory.FromPower<HbrDefenseDownPower>(),
        HoverTipFactory.FromPower<HbrAttackDownPower>(),HoverTipFactory.FromPower<FragilePower>(),
        HoverTipFactory.FromPower<HbrBindPower>(),HoverTipFactory.FromPower<HbrImprisonPower>(),
        HoverTipCompatibility.CreateKeyword(new LocString("cards","OPEN_THE_LOCK_HBRGO_SAKI.unlockTitle"),
            new LocString("cards","OPEN_THE_LOCK_HBRGO_SAKI.unlockDescription"),"HBR_ISUZU_UNLOCK")
    };
    public OpenTheLockHbrgoSaki() : base(2,CardType.Attack,TargetType.AllEnemies) { }
    public override bool TryModifyEnergyCostInCombat(CardModel card,decimal original,out decimal modified)
    {
        modified=original;
        if(!ReferenceEquals(card,this)||!IsMutable||Owner?.Creature?.CombatState==null||
            !HasUpgradeEffect(HellfireGate)||ThirtyOneEKeywordRules.GetPermanentDistinctCount(Owner)<3)return false;
        modified=Math.Max(0m,original-1m);return modified!=original;
    }
    protected override void AddExtraArgsToDescription(LocString text)
    { base.AddExtraArgsToDescription(text); text.Add("IsuzuDestruction",HasUpgradeEffect(AngryRift)?1.6m:1.2m); }
    protected override void OnUpgrade()
    {
        bool before=HasUpgradeEffect(AngryRift);base.OnUpgrade();
        if(!before&&HasUpgradeEffect(AngryRift))DynamicVars.Damage.UpgradeValueBy(HbrSkillPower.DamageUnits(26m));
    }
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        if(HasUpgradeEffect(EscapeArtist))
            foreach(PowerModel power in Owner.Creature.Powers.Where(power=>power is HbrBindPower or HbrImprisonPower).ToArray())
                await PowerCmd.Remove(power);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this,play)
            .TargetingAllOpponents(CombatState).WithHitCount(3).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        if(HasUpgradeEffect(AngryRift))await IsuzuOpenTheLockCombatHelper.ApplyAll<HbrDefenseDownPower>(context,this,30m);
        if(HasUpgradeEffect(Mirage))await IsuzuOpenTheLockCombatHelper.ApplyAll<FragilePower>(context,this,42m);
        if(HasUpgradeEffect(Mischief))await IsuzuOpenTheLockCombatHelper.Mischief(context,this);
        await IsuzuOpenTheLockCombatHelper.Unlock(context,this);
    }
}
