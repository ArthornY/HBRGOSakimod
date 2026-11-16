using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Cards;
public sealed class InnocentRequiemHbrgoSaki : ThirtyOneBGardenCard
{
    internal const int InspirationContagious = 0, Flash = 1, Moonlight = 2, FreezingCurse = 3,
        SpecialOrder = 4, UnchangingSmile = 5, SoulEvocare = 6;
    protected override int FlashEffectId => Flash;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<KozueSpiritPower>(), HoverTipFactory.FromPower<PhantomPower>(),
      HoverTipFactory.FromPower<FragilePower>(), HoverTipFactory.FromPower<HbrMoralePower>(),
      HoverTipFactory.FromPower<HbrCriticalRateUpPower>(), HoverTipFactory.FromPower<HbrCriticalDamageUpPower>() };
    public InnocentRequiemHbrgoSaki() : base(2, CardType.Skill, TargetType.Self) { }
    protected override void AddExtraArgsToDescription(LocString text)
    { base.AddExtraArgsToDescription(text); text.Add("SpiritHealthPercent", HasUpgradeEffect(SoulEvocare) ? 60 : 30); }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (HasUpgradeEffect(Moonlight))
            foreach (var ally in CombatState.Players.Where(player => player.Creature.IsAlive && player.Creature.Side == Owner.Creature.Side).ToArray())
                await PlayerCmd.GainEnergy(1m, ally);
        if (HasUpgradeEffect(SpecialOrder))
        {
            await PowerCmd.Apply<HbrMoralePower>(context, Owner.Creature, 2m, Owner.Creature, this);
            Owner.GetRelic<Overclock>()?.GainLevelProgress(15m);
        }
        await KozueSpiritHelper.Summon(context, this);
    }
}
