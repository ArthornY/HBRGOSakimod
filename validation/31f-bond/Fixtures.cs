using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
public sealed class FMember0:CardModel,IThirtyOneFMemberCard{public ThirtyOneFMember ThirtyOneFMember=>ThirtyOneFMember.YanagiMion;public FMember0():base(2,CardType.Skill,CardRarity.Common,TargetType.Self){}}
public sealed class FMember1:CardModel,IThirtyOneFMemberCard{public ThirtyOneFMember ThirtyOneFMember=>ThirtyOneFMember.MaruyamaKanata;public FMember1():base(1,CardType.Skill,CardRarity.Common,TargetType.Self){}}
public sealed class FMember2:CardModel,IThirtyOneFMemberCard{public ThirtyOneFMember ThirtyOneFMember=>ThirtyOneFMember.HanamuraShiki;public FMember2():base(3,CardType.Skill,CardRarity.Common,TargetType.Self){}}
public sealed class FMember3:CardModel,IThirtyOneFMemberCard{public ThirtyOneFMember ThirtyOneFMember=>ThirtyOneFMember.MatsuokaChiroru;public FMember3():base(4,CardType.Skill,CardRarity.Common,TargetType.Self){}}
public sealed class FMember4:CardModel,IThirtyOneFMemberCard{public ThirtyOneFMember ThirtyOneFMember=>ThirtyOneFMember.NatsumeInori;public FMember4():base(1,CardType.Skill,CardRarity.Common,TargetType.Self){}}
public sealed class FMember5:CardModel,IThirtyOneFMemberCard{public ThirtyOneFMember ThirtyOneFMember=>ThirtyOneFMember.KurosawaMaki;public FMember5():base(2,CardType.Skill,CardRarity.Common,TargetType.Self){}}
public sealed class FMemberX:CardModel,IThirtyOneFMemberCard{protected override bool HasEnergyCostX=>true;public ThirtyOneFMember ThirtyOneFMember=>ThirtyOneFMember.YanagiMion;public FMemberX():base(-1,CardType.Skill,CardRarity.Common,TargetType.Self){}}
public sealed class FMemberBlocked:CardModel,IThirtyOneFMemberCard{public ThirtyOneFMember ThirtyOneFMember=>ThirtyOneFMember.MaruyamaKanata;protected override bool IsPlayable=>false;public FMemberBlocked():base(0,CardType.Skill,CardRarity.Common,TargetType.Self){}}
public sealed class FOtherCard:CardModel{public FOtherCard():base(5,CardType.Skill,CardRarity.Common,TargetType.Self){}}

public sealed class FGlobalCostPower:PowerModel
{
    public decimal Offset; public decimal Factor=1m; public bool HighOnly,FloorHigh;public int Probes;
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
    public override bool TryModifyEnergyCostInCombat(CardModel card,decimal originalCost,out decimal modifiedCost)
    {Probes++;modifiedCost=card.Owner==Owner.Player&&(!HighOnly||card is FMember3)?(originalCost+Offset)*Factor:originalCost;if(FloorHigh&&card is FMember3)modifiedCost=System.Math.Max(2m,modifiedCost);return modifiedCost!=originalCost;}
}

public sealed class FNestedDrawPower:PowerModel
{
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
    public override System.Threading.Tasks.Task AfterCardDrawn(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context,CardModel card,bool fromHandDraw)=>EffectChecks.FNestedDraw(context,card,fromHandDraw);
}
