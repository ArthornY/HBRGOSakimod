using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class SlashIfCloseHbrgoSaki : ThirtyOneBGardenCard, IThirtyGMemberCard
{
    internal const int Flash = 0, MindsEye = 1, FullForce = 2, Desire = 3,
        KillingIntent = 4, MastersAdvice = 5, StillGrowing = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyGMember ThirtyGMember => ThirtyGMember.OgasawaraHisame;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(60m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<MindsEyePower>(), HoverTipFactory.FromPower<HbrDefenseDownPower>(), HoverTipFactory.FromPower<HbrAttackDownPower>() };
    public SlashIfCloseHbrgoSaki() : base(1, CardType.Attack, TargetType.AllEnemies) { }
    protected override void OnUpgrade() { base.OnUpgrade(); HisameCloseSlashCombatHelper.Invalidate(Owner); }
    protected override void AfterDeserialized() { base.AfterDeserialized(); HisameCloseSlashCombatHelper.Invalidate(Owner); }
    protected override void AfterDowngraded() { base.AfterDowngraded(); HisameCloseSlashCombatHelper.Invalidate(Owner); }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        using (HisameCloseSlashCombatHelper.BeginPreview(this, true))
            HbrSkillCardDescription.AddDamageParameters(this, text, "HisameGunDamage", HisameCloseSlashCombatHelper.GetBranchUnits(this, true));
        using (HisameCloseSlashCombatHelper.BeginPreview(this, false))
            HbrSkillCardDescription.AddDamageParameters(this, text, "HisameSwordDamage", HisameCloseSlashCombatHelper.GetBranchUnits(this, false));
    }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => HisameCloseSlashCombatHelper.Play(context, this, play);
}
