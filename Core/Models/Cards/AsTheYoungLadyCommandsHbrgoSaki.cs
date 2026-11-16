using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class AsTheYoungLadyCommandsHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneFMemberCard
{
    internal const int Flash = 0, Prepared = 1, Maintenance = 2, AlwaysAtHand = 3,
        HighBeams = 4, LadyFirst = 5, ElegantCleaning = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneFMember ThirtyOneFMember => ThirtyOneFMember.YanagiMion;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override TargetType TargetType => IsMutable && HasUpgradeEffect(Maintenance) &&
        Owner?.Creature?.CombatState?.Players.Count(player => player.Creature.IsAlive) > 1 ? TargetType.AnyAlly : TargetType.Self;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(76m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromKeyword(CardKeyword.Exhaust), HoverTipFactory.FromPower<HighDimensionalShellPower>(), EnergyHoverTip }
        .Append(HoverTipCompatibility.CreateRelicReference<Overclock>());
    public AsTheYoungLadyCommandsHbrgoSaki() : base(1, CardType.Skill, TargetType.Self) { }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("YanagiPiles", HasUpgradeEffect(AlwaysAtHand) ? "抽牌堆或弃牌堆中" : "抽牌堆中");
    }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => YanagiButlerCombatHelper.Play(context, this, play);
}
