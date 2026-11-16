using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("MARIA_REPENTANCE_POWER")]
public sealed class MariaRepentancePower:PowerModel,ICustomPower
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public int UpgradeMask{get;private set;}
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public int InspirationPercent{get;private set;}
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public bool LifesaveUsed{get;private set;}
    private RepentanceTimeHbrgoSaki _source;
    private HashSet<(int,object)> _turns=new();
    private Dictionary<(int,object),int> _blessings=new();
    public override PowerType Type=>PowerType.Buff;
    public override PowerStackType StackType=>PowerStackType.Counter;
    public override int DisplayAmount=>IsMutable?InspirationPercent:0;
    public string CustomPackedIconPath=>"res://images/powers/hbr_attack_up_power.png";
    public string CustomBigIconPath=>CustomPackedIconPath;
    public string CustomBigBetaIconPath=>CustomPackedIconPath;
    internal bool Has(int effect)=>(UpgradeMask&(1<<effect))!=0;
    internal void Register(RepentanceTimeHbrgoSaki card)
    {AssertMutable();_source=card;foreach(int effect in card.UpgradeEffectOrder.Take(card.CurrentUpgradeLevel))UpgradeMask|=1<<effect;}
    internal RepentanceTimeHbrgoSaki Source()
    {if(_source!=null)return _source;_source=(RepentanceTimeHbrgoSaki)ModelDb.Card<RepentanceTimeHbrgoSaki>().ToMutable();_source.Owner=Owner.Player;return _source;}
    private (int,object) Turn()=>(Owner.Player.PlayerCombatState?.TurnNumber??Owner.CombatState.RoundNumber,OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player));
    internal bool ClaimTurn()=>_turns.Add(Turn());
    internal bool ClaimBlessing()
    {var key=Turn();int count=_blessings.GetValueOrDefault(key);if(count>=2)return false;_blessings[key]=count+1;return true;}
    internal bool ClaimLifesave(){if(LifesaveUsed)return false;LifesaveUsed=true;return true;}
    internal void SetInspiration(int percent){AssertMutable();InspirationPercent=percent;InvokeDisplayAmountChanged();}
    public override Task AfterEnergyReset(Player player)=>player==Owner.Player
        ?MariaRepentanceCombatHelper.OnOwnTurnStart(new ThrowingPlayerChoiceContext(),player):Task.CompletedTask;
    public override LocString Description
    {get{var text=new LocString("powers",Id.Entry+(!IsMutable?".referenceDescription":".description"));text.Add("Inspiration",IsMutable?InspirationPercent:0);return text;}}
    protected override void AfterCloned(){base.AfterCloned();_source=null;_turns=new();_blessings=new();}
}
