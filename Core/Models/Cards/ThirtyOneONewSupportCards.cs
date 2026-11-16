using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ArpeggioHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { HbrSkillVars.Damage(500m, ValueProp.Move, divisor: 6m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[] { HoverTipFactory.FromPower<HbrComboSmallPower>() };
    public ArpeggioHbrgoSaki() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        await HbrComboPower.Grant<HbrComboSmallPower>(context, Owner.Creature, Owner.Creature, this);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeDamageByPercent(200m, divisor: 6m);
}

public sealed class MimicShootingStarSongHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new IntVar("CriticalRate", 50m), new IntVar("CriticalDamage", 50m), new IntVar("Turns", 3m)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<HbrCriticalRateUpPower>(), HoverTipFactory.FromPower<HbrCriticalDamageUpPower>()
    };
    public MimicShootingStarSongHbrgoSaki() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await HbrEffectApplication.Apply<HbrCriticalRateUpPower>(context, Owner.Creature,
            DynamicVars["CriticalRate"].BaseValue, Owner.Creature, this, DynamicVars["Turns"].IntValue);
        await HbrEffectApplication.Apply<HbrCriticalDamageUpPower>(context, Owner.Creature,
            DynamicVars["CriticalDamage"].BaseValue, Owner.Creature, this, DynamicVars["Turns"].IntValue);
    }
    protected override void OnUpgrade() => DynamicVars["Turns"].UpgradeValueBy(1m);
}

public sealed class MimicHeartFlutterSlideHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<HbrMoralePower>(2m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[] { HoverTipFactory.FromPower<HbrMoralePower>() };
    public MimicHeartFlutterSlideHbrgoSaki() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        foreach (Creature ally in TeamCombatHelper.GetLivingAllies(Owner.Creature))
            await PowerCmd.Apply<HbrMoralePower>(context, ally, DynamicVars["HbrMoralePower"].BaseValue,
                Owner.Creature, this);
    }
    protected override void OnUpgrade() => DynamicVars["HbrMoralePower"].UpgradeValueBy(1m);
}

public sealed class MimicDivineCraftHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(400m, ValueProp.Move, divisor: 3m), new PowerVar<HbrCalamityPower>(20m)
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<ReptureBowPower>(), HoverTipFactory.FromPower<HbrCalamityPower>()
    };
    public MimicDivineCraftHbrgoSaki() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        Creature[] targets = Owner.Creature.CombatState.GetOpponentsOf(Owner.Creature)
            .Where(creature => creature.IsHittable).ToArray();
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .TargetingAllOpponents(Owner.Creature.CombatState).WithHitFx("vfx/vfx_attack_slash").Execute(context);
        foreach (Creature target in targets)
            if (target.IsAlive) await PowerCmd.Apply<HbrCalamityPower>(context, target,
                DynamicVars["HbrCalamityPower"].BaseValue, Owner.Creature, this);
    }
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeDamageByPercent(50m);
        DynamicVars["HbrCalamityPower"].UpgradeValueBy(10m);
    }
}

public sealed class MimicWillActivationHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<ReptureDualSwordsPower>(), HoverTipFactory.FromPower<HbrComboLargePower>()
    };
    public MimicWillActivationHbrgoSaki() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await HbrComboPower.Grant<HbrComboLargePower>(context, Owner.Creature, Owner.Creature, this);
        await CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
