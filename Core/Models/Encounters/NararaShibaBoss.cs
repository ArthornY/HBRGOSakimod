using System.Collections.Generic;
using System.Globalization;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

public sealed class NararaShibaBoss : EncounterModel, IHbrgoEncounter
{
    internal const int StoryContinuationVersion = 1;
    internal HbrStoryCombatState StoryState { get; set; }
    internal int InitialFloor { get; set; }

    public override Dictionary<string, string> SaveCustomState()
    {
        var state = new Dictionary<string, string>();
        if (StoryState != null) state["HbrStory"] = StoryState.Serialize();
        if (InitialFloor > 0) state["NararaInitialFloor"] = InitialFloor.ToString(CultureInfo.InvariantCulture);
        return state;
    }

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        StoryState = state != null && state.TryGetValue("HbrStory", out string json) ? HbrStoryCombatState.Deserialize(json) : null;
        InitialFloor = state != null && state.TryGetValue("NararaInitialFloor", out string floor)
            && int.TryParse(floor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0
                ? value : 0;
    }

    public override RoomType RoomType => RoomType.Boss;

    public override string BossNodePath =>
        "res://images/map/boss_nodes/flat_hand";

    public override MegaSkeletonDataResource BossNodeSpineResource => null;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<NararaShibaBossMonster>() };

    public override float GetCameraScaling()
    {
        return 1f;
    }

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var monster = (NararaShibaBossMonster)ModelDb.Monster<NararaShibaBossMonster>().ToMutable();
        monster.InitialFloor = InitialFloor;
        return new (MonsterModel, string)[]
        {
            (monster, null)
        };
    }
}
