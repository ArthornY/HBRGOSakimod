using System;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Debug;

namespace HBRGOSakiMod.Core.DevConsole;

public sealed class HbrMemoryUpgradeStoryPreviewConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "hbrmemory";
    public override string Args => "preview <actorId> <1-7> <31o|30a|hotaru>";
    public override string Description => "Preview an authored Memory Garden upgrade story locally, without recording Started or Seen.";
    public override bool DebugOnly => false;
    public override bool IsNetworked => false;
    public override CmdResult Process(Player player, string[] args)
    {
        if (player == null || !LocalContext.IsMe(player) || args.Length != 4 ||
            !args[0].Equals("preview", StringComparison.OrdinalIgnoreCase) || !int.TryParse(args[2], out int level) ||
            !HbrMemoryUpgradeStoryPerspectives.TryParse(args[3], out var perspective))
            return new CmdResult(false, "Usage: hbrmemory " + Args);
        string actorId = args[1].ToLowerInvariant();
        if (!HbrMemoryUpgradeStoryCatalog.TryFind(actorId, perspective, level, out _))
            return new CmdResult(false, "No authored story for this character, perspective and level.");
        // This is a separate, non-networked command. Do not reuse the existing
        // networked hbrgosakimod command or enter the persistent upgrade queue.
        NDevConsole.Instance?.Hide();
        if (!HbrMemoryUpgradeStoryPresentation.CanPlayLocal(player))
            return new CmdResult(false, "A local story cannot open on the current screen.");
        return new CmdResult(Preview(player, perspective, actorId, level), true, "Local memory preview.");
    }
    private static async Task Preview(Player player, HbrMemoryUpgradeStoryPerspective perspective, string actorId, int level)
    {
        await HbrMemoryUpgradeStoryPresentation.PlayLocalAsync(player, perspective, actorId, level);
    }
}
