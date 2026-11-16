using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class IWillNotFallHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneEMemberCard
{
    internal const int Flash = 0, SummerGrace = 1, DelightfulSprint = 2, NewDay = 3,
        EcstaticPain = 4, SweetHealing = 5, NeverFall = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneEMember ThirtyOneEMember => ThirtyOneEMember.OshimaMuua;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(77m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<HbrFaithPower>(), HoverTipFactory.FromPower<MuuaIWillNotFallPower>(),
        HoverTipFactory.FromPower<MindsEyePower>(), HoverTipFactory.FromPower<HighDimensionalShellPower>(),
        HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrCriticalDamageUpPower>()
    }.Append(HoverTipCompatibility.CreateRelicReference<Overclock>());
    public IWillNotFallHbrgoSaki() : base(1, CardType.Power, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!HbrBattleStatsHelper.IsEnabled(Owner)) return;
        await PowerCmd.Apply<MuuaIWillNotFallPower>(context, Owner.Creature, 1m, Owner.Creature, this);
        Owner.Creature.GetPower<MuuaIWillNotFallPower>()?.RegisterSource(this);
    }
}
