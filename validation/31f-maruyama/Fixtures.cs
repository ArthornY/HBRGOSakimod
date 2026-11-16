using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
public class ExtraServingProbe:CardModel,IExtraServingCard
{
    public static int Plays;
    public override string Title=>"Extra Serving native probe";
    public ExtraServingProbe():base(4,CardType.Skill,CardRarity.Common,TargetType.Self){}
    protected override Task OnPlay(PlayerChoiceContext c,CardPlay p){Plays++;return Task.CompletedTask;}
}
public sealed class ExtraServingBlocked:ExtraServingProbe{protected override bool IsPlayable=>false;}
public sealed class ExtraServingX:ExtraServingProbe{protected override bool HasEnergyCostX=>true;}
public sealed class ExtraServingStars:ExtraServingProbe{public override int CanonicalStarCost=>2;}
public sealed class ExtraServingHookPower:PowerModel
{
    public bool Block,PayStars;public int Replays;
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
    public override bool ShouldPlay(CardModel card,AutoPlayType type)=>!Block;
    public override bool ShouldPayExcessEnergyCostWithStars(Player player)=>PayStars;
    public override int ModifyCardPlayCount(CardModel card,MegaCrit.Sts2.Core.Entities.Creatures.Creature target,int amount)=>amount+Replays;
    public override Task AfterEnergySpent(CardModel card,int amount){EffectChecks.MPaid.Add(amount);return Task.CompletedTask;}
}
