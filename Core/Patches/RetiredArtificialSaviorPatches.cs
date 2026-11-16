using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.DailyRun;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace HBRGOSakiMod.Core.Patches;

// The base 人造救世主 (HBRGO_SAKI) left the game on 2026-11-09 (user: 「话说游戏里还保留着的
// 人造救世主角色删掉吧一起」). It stays registered, so old saves, run history and its
// cards still load, and 31O and 30A keep inheriting from it. It was already hidden
// from the native character select and its random roll; the call sites below are
// where it still showed up or was counted as a character to have played.
internal static class HbrgoRetiredCharacters
{
    internal static ModelId RetiredId => ModelDb.GetId(typeof(HbrgoSaki));

    internal static bool IsRetired(CharacterModel character) =>
        character != null && character.GetType() == typeof(HbrgoSaki);

    internal static bool IsRetired(ModelId id) => id != null && id == RetiredId;

    internal static IEnumerable<CharacterModel> WithoutRetired(
        IEnumerable<CharacterModel> characters) =>
        (characters ?? Enumerable.Empty<CharacterModel>()).Where(character => !IsRetired(character));

    // Stands in for ModelDb.AllCharacters at the patched call sites.
    internal static IEnumerable<CharacterModel> SelectableCharacters() =>
        WithoutRetired(ModelDb.AllCharacters);

    // Stands in for Progress.CharacterStats.Count in the post-run epoch checks: stats
    // left by old 人造救世主 runs do not count as one of the characters played.
    internal static int CountStatsWithoutRetired(
        IReadOnlyCollection<KeyValuePair<ModelId, CharacterStats>> stats) =>
        stats?.Count(entry => !IsRetired(entry.Key)) ?? 0;
}

[HarmonyPatch]
internal static class RetiredArtificialSaviorCallSitePatch
{
    private static readonly MethodInfo AllCharactersGetter =
        AccessTools.PropertyGetter(typeof(ModelDb), nameof(ModelDb.AllCharacters));

    private static readonly MethodInfo SelectableCharacters =
        AccessTools.Method(typeof(HbrgoRetiredCharacters), nameof(HbrgoRetiredCharacters.SelectableCharacters));

    private static readonly MethodInfo CharacterStatsGetter =
        AccessTools.PropertyGetter(typeof(ProgressState), nameof(ProgressState.CharacterStats));

    private static readonly MethodInfo CharacterStatsCount =
        AccessTools.PropertyGetter(
            typeof(IReadOnlyCollection<KeyValuePair<ModelId, CharacterStats>>),
            nameof(IReadOnlyCollection<KeyValuePair<ModelId, CharacterStats>>.Count));

    private static readonly MethodInfo CountStatsWithoutRetired =
        AccessTools.Method(typeof(HbrgoRetiredCharacters), nameof(HbrgoRetiredCharacters.CountStatsWithoutRetired));

    // Every target is looked up by name: RollModifiers exists only on stable and
    // ModifierModel.Pick2Good1Bad only on the v0.111.0 beta, which moved the daily
    // and custom-run modifier roll there.
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        MethodBase[] targets =
        {
            // Custom run: its character buttons and the 「人造救世主卡牌」 modifier.
            AccessTools.DeclaredMethod(typeof(NCustomRunScreen), "InitCharacterButtons"),
            EnumeratorMoveNext(AccessTools.DeclaredMethod(typeof(NCustomRunModifiersList), "GetAllModifiers")),
            // Daily run: the rolled characters and the rolled character-cards modifier.
            AccessTools.DeclaredMethod(typeof(NDailyRunScreen), "SetupLobbyParams"),
            AccessTools.DeclaredMethod(typeof(NDailyRunScreen), "RollModifiers"),
            AccessTools.DeclaredMethod(typeof(ModifierModel), "Pick2Good1Bad"),
            // Orobas offers one Sea Glass per character; its cards come from that pool.
            AccessTools.DeclaredPropertyGetter(typeof(Orobas), "SeaGlassOptions"),
            // Counts that compare against "every character".
            AccessTools.DeclaredMethod(typeof(Kaleidoscope), nameof(Kaleidoscope.IsAllowedAtNeow)),
            AccessTools.DeclaredMethod(typeof(ProgressSaveManager), "UpdateEpochsPostRun"),
        };
        return targets.Where(target => target != null);
    }

    private static MethodBase EnumeratorMoveNext(MethodBase iterator) =>
        iterator == null ? null : AccessTools.EnumeratorMoveNext(iterator);

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        List<CodeInstruction> code = instructions.ToList();
        int characters = 0;
        for (int index = 0; index < code.Count; index++)
        {
            CodeInstruction instruction = code[index];
            if (instruction.Calls(AllCharactersGetter))
            {
                // Rewrite in place so labels and exception blocks stay attached.
                instruction.opcode = OpCodes.Call;
                instruction.operand = SelectableCharacters;
                characters++;
            }
            else if (instruction.Calls(CharacterStatsCount) && index > 0 &&
                     code[index - 1].Calls(CharacterStatsGetter))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = CountStatsWithoutRetired;
            }
        }

        if (characters == 0)
        {
            Log.Warn(
                $"[HBRGO] {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name} no longer reads " +
                "ModelDb.AllCharacters; the retired 人造救世主 is not filtered there.");
        }

        return code;
    }
}
