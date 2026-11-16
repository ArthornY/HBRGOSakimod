using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("VRITIKA_CALCULATION_POWER")]
public sealed class VritikaCalculationPower:PowerModel,ICustomPower
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]public string SerialData{get;private set;}=string.Empty;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]public string NormalSequences{get;private set;}=string.Empty;
    private NumbersAreTheSourceHbrgoSaki _source;
    public override PowerType Type=>PowerType.Buff;
    public override PowerStackType StackType=>PowerStackType.Counter;
    public override int DisplayAmount=>IsMutable?VritikaCalculationCombatHelper.Display(Owner?.Player).Progress:0;
    public string CustomPackedIconPath=>"res://images/packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png";
    public string CustomBigIconPath=>CustomPackedIconPath;
    public string CustomBigBetaIconPath=>CustomPackedIconPath;
    internal BigInteger Serial=>BigInteger.TryParse(SerialData,out var value)?BigInteger.Max(0,value):BigInteger.Zero;
    internal void Save(string normal,BigInteger serial){AssertMutable();NormalSequences=normal;SerialData=serial.ToString(System.Globalization.CultureInfo.InvariantCulture);InvokeDisplayAmountChanged();}
    internal void RefreshDisplay()=>InvokeDisplayAmountChanged();
    internal void Source(NumbersAreTheSourceHbrgoSaki card){AssertMutable();_source=card;}
    internal NumbersAreTheSourceHbrgoSaki Source()
    {if(_source!=null)return _source;_source=(NumbersAreTheSourceHbrgoSaki)ModelDb.Card<NumbersAreTheSourceHbrgoSaki>().ToMutable();_source.Owner=Owner.Player;return _source;}
    public override Task BeforeCardPlayed(CardPlay cardPlay){VritikaCalculationCombatHelper.BeforePlay(this,cardPlay);return Task.CompletedTask;}
    public override Task AfterCardPlayed(PlayerChoiceContext context,CardPlay cardPlay)=>VritikaCalculationCombatHelper.AfterPlay(context,this,cardPlay);
    public override Task AfterEnergyReset(Player player)
    {if(player==Owner.Player)InvokeDisplayAmountChanged();return Task.CompletedTask;}
    public override LocString Description
    {
        get
        {
            var text=new LocString("powers",Id.Entry+(!IsMutable?".referenceDescription":".description"));
            var display=IsMutable?VritikaCalculationCombatHelper.Display(Owner?.Player):default;
            text.Add("Sequence",display.Sequence??string.Empty);text.Add("Completed",display.Progress);text.Add("Status",display.Status??string.Empty);return text;
        }
    }
    protected override void AfterCloned(){base.AfterCloned();_source=null;}
}
