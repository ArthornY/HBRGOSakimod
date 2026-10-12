using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

public sealed class FlatHandBoss : EncounterModel, IHbrgoEncounter
{
    private const string NararaUnlockStateKey = "UnlockedNararaShibaThisRun";
    private bool _unlockedNararaShibaThisRun;

    public override Dictionary<string, string> SaveCustomState()
    {
        // Pre-finished rooms persist encounter state, not the defeated monsters'
        // SavedProperties. Keep the first-unlock exclusion after a save/reload.
        bool unlocked = _unlockedNararaShibaThisRun ||
            (HaveMonstersBeenGenerated && MonstersWithSlots.Any(monster =>
                monster.Item1 is FlatHand { UnlockedNararaShibaThisRun: true }));
        return unlocked
            ? new Dictionary<string, string> { [NararaUnlockStateKey] = bool.TrueString }
            : new Dictionary<string, string>();
    }

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        _unlockedNararaShibaThisRun = state != null &&
            state.TryGetValue(NararaUnlockStateKey, out string value) &&
            bool.TryParse(value, out bool unlocked) && unlocked;
        if (!HaveMonstersBeenGenerated) return;

        foreach (var (monster, _) in MonstersWithSlots)
        {
            if (monster is FlatHand flatHand)
                flatHand.UnlockedNararaShibaThisRun = _unlockedNararaShibaThisRun;
        }
    }

    public override RoomType RoomType => RoomType.Boss;

    public override string BossNodePath =>
        "res://images/map/boss_nodes/flat_hand";

    public override MegaSkeletonDataResource BossNodeSpineResource => null;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<FlatHand>() };

    public override float GetCameraScaling()
    {
        return 1f;
    }

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var flatHand = (FlatHand)ModelDb.Monster<FlatHand>().ToMutable();
        // Native CombatRoom.FromSerializable loads the encounter before it
        // generates the finished room's monsters.
        flatHand.UnlockedNararaShibaThisRun = _unlockedNararaShibaThisRun;
        return new (MonsterModel, string)[]
        {
            (flatHand, null)
        };
    }
}
