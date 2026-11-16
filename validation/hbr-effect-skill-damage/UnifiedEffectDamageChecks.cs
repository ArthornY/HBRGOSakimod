using System.Collections;
using System.Reflection;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static partial class EffectChecks
{
    static async Task UnifiedEffectDamageCases()
    {
        Reset(seed:FindSeed(true));
        HbrCriticalDamageUpPower critical = Power<HbrCriticalDamageUpPower>(Hero,3);
        Invoke(typeof(HbrCriticalDamageUpPower).BaseType!.GetMethod("ConfigureEffect",F)!,critical,50m,null);
        object scope=Call("HbrCriticalDamageContext","Begin",Hero,null,ValueProp.Move)!;
        Set(scope,"Sample",0);
        var results=new List<DamageResult>();
        Call("ItsMygoIncomingDamageCapture","Begin",results);
        try
        {
            Equal(Actual(Enemy,Hero,6m,ValueProp.Move,null),12000m,"unified critical output retains exact original 150% plus 50% factor");
            object stack=Helper("ItsMygoIncomingDamageCapture").GetField("_scopes",F)!.GetValue(null)!;
            object captureScope=stack.GetType().GetMethod("Peek")!.Invoke(stack,null)!;
            object capture=captureScope.GetType().GetProperty("Captured",F)!.GetValue(captureScope)!;
            Equal((decimal)capture.GetType().GetProperty("Amount",F)!.GetValue(capture)!,12000m,
                "reflection raw ledger includes the exact critical bonus once");
        }
        finally{Call("ItsMygoIncomingDamageCapture","End");Call("HbrCriticalDamageContext","Close",scope);}
        Clean("unified critical ledger");

        Reset();
        using((IDisposable)Call("HbrEffectSkillDamageContext","BeginWorld",Hero,Enemy,null,1000m,ValueProp.Unpowered,Enemy)!)
            await Command(Enemy,1000m,ValueProp.Unpowered,null,new[]{Hero});
        decimal expected=decimal.Floor(1000m*(1m+(150m-100m)/140m));
        Equal(Hits.Single().Final,expected,"enemy world damage normalizes with incoming100 units rather than hero1000 units");
        Check(ReferenceEquals(Hits.Single().Dealer,Enemy)&&Hits.Single().Raw==1000m,
            "enemy world reflection preserves native attribution and raw request");
        Clean("enemy world effect");

        Reset();
        using(Begin(Enemy,null,null,6m,ValueProp.Unpowered,Enemy))
            await Command(null,6m,ValueProp.Unpowered,null,new[]{Enemy});
        Equal(Hits.Single().Final,600m,"enemy confusion skill uses enemy E as its defensive scalar and incoming100 units");
        Check(Hits.Single().Dealer==null,"enemy confusion arithmetic source does not overwrite its native null dealer");
        Clean("enemy self effect");
    }
}
