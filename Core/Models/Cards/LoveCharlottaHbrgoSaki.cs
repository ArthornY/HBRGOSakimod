using System;
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
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class LoveCharlottaHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneXMemberCard
{
    internal const int Flash = 0, GrenadeForYou = 1, PureKiss = 2, EternalVow = 3,
        PallasCatDoll = 4, Jealousy = 5, EngagementRing = 6;
    protected override int FlashEffectId => -1;
    protected override bool HasEnergyCostX => true;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool VowGrantedThisBattle { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool DollGrantedThisBattle { get; private set; }
    public ThirtyOneXMember ThirtyOneXMember => ThirtyOneXMember.CharlottaSkopovskaya;
    public override TargetType TargetType => HasUpgradeEffect(GrenadeForYou) ? TargetType.AllEnemies : TargetType.AnyEnemy;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(64m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HbrEternalVowPower>(), HoverTipFactory.FromPower<HbrDefenseDownPower>(),
        HoverTipFactory.FromPower<FragilePower>(), HoverTipFactory.FromPower<CharlottaEngagementRingPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Retain), HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    };
    public LoveCharlottaHbrgoSaki() : base(0, CardType.Attack, TargetType.AnyEnemy) { }
    internal bool TakeVow()
    { if (VowGrantedThisBattle) return false; VowGrantedThisBattle = true; return true; }
    internal bool TakeDoll()
    { if (DollGrantedThisBattle) return false; DollGrantedThisBattle = true; return true; }
    internal void ClearBattleReceipts() => VowGrantedThisBattle = DollGrantedThisBattle = false;
    public override Task BeforeCombatStart()
    { CharlottaLoveCombatHelper.BeforeCombatStart(this); return Task.CompletedTask; }
    protected override void AfterCloned()
    { base.AfterCloned(); VowGrantedThisBattle = DollGrantedThisBattle = false; }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        int paid = IsMutable && Owner?.PlayerCombatState != null ? Math.Max(0, EnergyCost.GetAmountToSpend()) : 1;
        int x = CharlottaLoveCombatHelper.EffectiveX(this, paid);
        HbrSkillCardDescription.AddDamageParameters(this, text, "CharlottaDamage", HbrSkillPower.DamageUnits(64m * x));
        text.Add("CharlottaX", x);
    }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => CharlottaLoveCombatHelper.Play(context, this, play);
}
