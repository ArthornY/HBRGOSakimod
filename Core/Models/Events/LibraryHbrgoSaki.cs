using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Story;

namespace HBRGOSakiMod.Core.Models.Events;

[CustomID("LIBRARY_HBRGO_SAKI")]
public sealed class LibraryHbrgoSaki : HbrStoryEvent
{
    public override string DefinitionPath => "res://story/events/library_hbrgo_saki.json";
}
