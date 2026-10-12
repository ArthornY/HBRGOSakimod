using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Story;

internal static class HbrMemoryFragmentPool
{
    internal static IReadOnlyList<Type> StoryTypes { get; } = typeof(HbrStoryEvent).Assembly.GetTypes()
        .Where(type => !type.IsAbstract && typeof(HbrStoryEvent).IsAssignableFrom(type))
        .OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();

    internal static HbrStoryDefinition GetDefinition(Type storyType)
    {
        // Read only the generated constant path. Running an EventModel constructor
        // here could re-enter ModelDb.GetEntry while its identity is being resolved.
        HbrStoryEvent shell = (HbrStoryEvent)RuntimeHelpers.GetUninitializedObject(storyType);
        return HbrStoryDefinitionStore.Load(shell.DefinitionPath);
    }

    internal static bool IsMemoryFragment(EventModel eventModel) => eventModel is HbrStoryEvent story
        && story.Definition.EventPool == HbrMemoryFragmentPoolRules.PoolId;

    internal static HbrStoryEvent Choose(RunState runState) => HbrMemoryFragmentPoolRules.Choose(
        runState.Players.Select(player => player.Character.GetType().Name),
        GetMemories(),
        story => story.IsAllowedForStoryPool(runState, allowReplay: true),
        story => story.Definition.Id, VisitedStoryIds(runState),
        () => runState.Rng.Niche.NextFloat());

    internal static HbrStoryEvent ChooseForMemoryNode(RunState runState) => HbrMemoryFragmentPoolRules.ChooseGuaranteed(
        runState.Players.Select(player => player.Character.GetType().Name),
        GetMemories(),
        story => story.IsAllowedForStoryPool(runState, allowReplay: true),
        story => story.Definition.Id, VisitedStoryIds(runState),
        () => runState.Rng.Niche.NextFloat());

    private static IEnumerable<HbrStoryEvent> GetMemories() => StoryTypes
        .Select(type => ModelDb.GetById<HbrStoryEvent>(ModelDb.GetId(type)))
        // Dedicated-transition stories also record character encounters, but
        // must never absorb weight redistributed within the random fragment pool.
        .Where(story => IsMemoryFragment(story) && !story.IsActEndingDoor &&
            story.Definition.Id != HbrMemoryGardenRestStory.EventId)
        .OrderBy(story => story.Id.Entry, StringComparer.Ordinal);

    internal static IEnumerable<string> VisitedStoryIds(IRunState runState) => runState.MapPointHistory
        // New room selection happens before its history entry is appended: the
        // last entry is the previous node and must participate in redistribution.
        // One event visit may contain combat/return rooms; count it once per node.
        .SelectMany(act => act).SelectMany(point => point.Rooms
            .Where(room => room.RoomType == RoomType.Event && room.ModelId != null)
            .Select(room => HbrStoryDefinition.NormalizeEventId(room.ModelId.Entry))
            .Distinct(StringComparer.Ordinal));
}
