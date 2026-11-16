using System.Security.Cryptography;
using System.Text.Json;
using HBRGOSakiMod.Core.Nodes.Events.Story;
using System.Collections.Generic;

static class ClockChecks
{
    static readonly List<string> Passed=new();
    static void Check(bool value,string name){if(!value)throw new Exception(name);Passed.Add(name);}
    static async Task Cancelled(Task task){try{await task;throw new Exception("Expected cancellation");}catch(OperationCanceledException){}}
    public static async Task Main(string[] args)
    {
        string? error=null;
        try
        {
            using var clock=new HbrStoryAnimationClock();float x=-1;
            Task zero=clock.Animate("zero",0,t=>x=t);Check(zero.IsCompletedSuccessfully&&x==1,"zero-duration skip applies final endpoint immediately");
            Task first=clock.Animate("move",1,t=>x=t);Check(x==0&&!first.IsCompleted,"positive duration starts at zero without wall-clock timer");
            clock.Tick(.25);Check(x==.25f&&!first.IsCompleted,"controlled tick advances exact quarter endpoint");
            clock.Paused=true;clock.Tick(20);Check(x==.25f&&!first.IsCompleted,"pause/cover freezes animation and waits");
            clock.Paused=false;clock.Tick(.75);await first;Check(x==1,"resumed clock reaches final endpoint once");
            var cancellation=new CancellationTokenSource();Task wait=clock.Wait(1,cancellation.Token);cancellation.Cancel();await Cancelled(wait);Check(wait.IsCanceled,"cancelled wait settles without a future frame");clock.Tick(1);
            Task old=clock.Animate("same",4,t=>x=t);Task next=clock.Animate("same",2,t=>x=10+t);await Cancelled(old);Check(old.IsCanceled&&!next.IsCompleted,"new command cancels only prior property owner");clock.Tick(2);await next;Check(x==11,"replacement writes new endpoint without stale callback");
            float a=0,b=0;Task ta=clock.Animate("a",3,t=>a=t),tb=clock.Animate("b",3,t=>b=t);clock.Cancel("a");await Cancelled(ta);clock.Tick(1);Check(a==0&&b>0&&!tb.IsCompleted,"unrelated property animation remains independent");clock.CompleteAll();await tb;Check(b==1,"fast forward commits all remaining persistent endpoints");
            Task blocking=clock.Wait(4);clock.CompleteAll();await blocking;Check(blocking.IsCompletedSuccessfully,"fast forward releases blocked fade/wait locally");
            var disposed=new HbrStoryAnimationClock();Task pending=disposed.Wait(50);disposed.Dispose();await Cancelled(pending);Check(pending.IsCanceled,"reader exit cancels pending jobs without timer leakage");disposed.Dispose();Check(true,"dispose is idempotent");
            Task after=disposed.Wait(1);await Cancelled(after);Check(after.IsCanceled,"closed reader cannot schedule a new job");
            int calls=0;using var c2=new HbrStoryAnimationClock();Task end=c2.Animate("end",1,t=>{if(t==1)calls++;});c2.Tick(100);await end;c2.Tick(100);c2.CompleteAll();Check(calls==1,"long frame commits endpoint only once");
            Task stop=c2.Animate("negative",1,t=>x=t);c2.Tick(-2);Check(x==0&&!stop.IsCompleted,"negative delta cannot advance local time");c2.CompleteAll();await stop;
            using var already=new CancellationTokenSource();already.Cancel();int effects=0;Task never=c2.Animate("never",0,t=>effects++,already.Token);await Cancelled(never);Check(effects==0,"pre-cancelled command cannot mutate scene even at zero duration");
        }
        catch(Exception e){error=e.ToString();}
        var result=new{ok=error==null,checks=Passed.Count,passed=Passed,error,boundary="Pure explicitly ticked visual clock; no game engine, native gameplay, real-time sleeping or RNG."};
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);File.WriteAllText(args[0],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count);if(error!=null)throw new Exception(error);
    }
}
