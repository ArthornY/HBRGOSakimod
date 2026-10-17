using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

[CustomID("HOTARU_INCOMPLETE_CHORD")]
public sealed class HotaruIncompleteChord : HotaruCannonCard
{
    public HotaruIncompleteChord() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new IntVar("Percent", 15m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruIncompleteChordPower>() };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        PowerCmd.Apply<HotaruIncompleteChordPower>(context, Owner.Creature, DynamicVars["Percent"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars["Percent"].UpgradeValueBy(5m);
}

[CustomID("HOTARU_TRIPLE_SILENCE")]
public sealed class HotaruTripleSilence : HotaruCannonCard
{
    public HotaruTripleSilence() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new IntVar("Percent", 50m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HotaruTripleSilencePower>() };
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        PowerCmd.Apply<HotaruTripleSilencePower>(context, Owner.Creature, DynamicVars["Percent"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars["Percent"].UpgradeValueBy(25m);
}

[CustomID("HOTARU_ECHOES_UNFINISHED")]
public sealed class HotaruEchoesUnfinished : HotaruCannonCard
{
    public HotaruEchoesUnfinished() : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move), new IntVar("ExtraHits", 1m)
    };
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        int retired = Owner.GetRelic<Millsage>()?.RetiredCannonCount ?? 0;
        int hits = (int)Math.Clamp((long)ResolveEnergyXValue() + DynamicVars["ExtraHits"].IntValue + retired, 0L, int.MaxValue);
        if (hits == 0) return;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitCount(hits).WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}
