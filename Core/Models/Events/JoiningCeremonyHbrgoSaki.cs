using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Models.Events;

// Read through the shared story reader like the memory fragments. The manual's
// ModifyNextEvent still delivers it as act one's first event, never an event
// pool, and every player keeps a personal reading and choice as before.
[CustomID("JOINING_CEREMONY_HBRGO_SAKI")]
public sealed class JoiningCeremonyHbrgoSaki : HbrStoryEvent
{
    public override string DefinitionPath => "res://story/events/joining_ceremony_hbrgo_saki.json";

    public override bool IsShared => false;

    public override bool IsAllowed(IRunState runState) => false;
}
