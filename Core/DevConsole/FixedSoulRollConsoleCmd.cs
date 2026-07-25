using System;
using System.Linq;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.DevConsole;

public sealed class FixedSoulRollConsoleCmd : AbstractConsoleCmd
{
    private const string _subcommand = "watashikakamininanimashu";

    public override string CmdName => "hbrgosakimod";

    public override string Args => $"{_subcommand} <true|false>";

    public override string Description => "开启或关闭安科之魂掷骰固定为100。";

    public override bool DebugOnly => false;

    public override bool IsNetworked => true;

    public override CmdResult Process(Player player, string[] args)
    {
        if (args.Length != 2 ||
            !args[0].Equals(_subcommand, StringComparison.OrdinalIgnoreCase) ||
            !bool.TryParse(args[1], out bool enabled))
        {
            return new CmdResult(false, $"Usage: {CmdName} {_subcommand} <true|false>");
        }

        if (player == null)
        {
            return new CmdResult(false, "This command only works during a run.");
        }

        SoulOfAnchor[] souls = player.Relics.OfType<SoulOfAnchor>().ToArray();
        if (souls.Length == 0)
        {
            return new CmdResult(false, "The player does not have Soul of Anchor.");
        }

        foreach (SoulOfAnchor soul in souls)
        {
            soul.SetRollsFixedToOneHundred(enabled);
        }

        return new CmdResult(
            true,
            enabled
                ? "All Soul of Anchor rolls are now fixed at 100."
                : "All Soul of Anchor rolls are random again.");
    }
}
