using System;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.DevConsole;

public sealed class FixedSoulRollConsoleCmd : AbstractConsoleCmd
{
    private const string _subcommand = "watashikakamininanimashu";
    // Only for typing by hand: the OD gauge sends HbrOverclockNetActions instead
    // (2026-11-09). Typed, it is a console command and excludes the run from the upload.
    public const string ActivateOverclockSubcommand = "__activate_overclock";
    public const string SummonRedCrimsonSubcommand =
        "__summon_red_crimson";

    public override string CmdName => "hbrgosakimod";

    public override string Args => $"{_subcommand} <true|false>";

    public override string Description => "开启或关闭“掷骰”词条出目固定为100。";

    public override bool DebugOnly => false;

    public override bool IsNetworked => true;

    public override CmdResult Process(Player player, string[] args)
    {
        if (args.Length == 1 &&
            args[0].Equals(
                SummonRedCrimsonSubcommand,
                StringComparison.OrdinalIgnoreCase))
        {
            return SummonRedCrimson(player);
        }

        if (args.Length == 2 &&
            args[0].Equals(ActivateOverclockSubcommand, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(args[1], out int requestedLevel))
        {
            return ActivateOverclock(player, requestedLevel);
        }

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

        if (!RollKeywordRules.IsRollEnabled(player))
        {
            return new CmdResult(false, "The player does not have the Roll keyword.");
        }

        RollKeywordCombatState.SetRollsFixedToOneHundred(
            player,
            enabled);

        return new CmdResult(
            true,
            enabled
                ? "All Roll keyword results are now fixed at 100."
                : "All Roll keyword results are random again.");
    }

    private static CmdResult SummonRedCrimson(Player player)
    {
        if (player == null)
        {
            return new CmdResult(
                false,
                "This command only works during a run.");
        }

        RestorationMoment relic =
            player.GetRelic<RestorationMoment>();
        if (relic == null)
        {
            return new CmdResult(
                false,
                "The player does not have Restoration Moment.");
        }

        if (!relic.TryBeginSummon(out Task summonTask))
        {
            return new CmdResult(
                false,
                "Red Crimson cannot be summoned right now.");
        }

        return new CmdResult(
            summonTask,
            true,
            "Red Crimson summoned.");
    }

    private static CmdResult ActivateOverclock(Player player, int requestedLevel)
    {
        if (player == null)
        {
            return new CmdResult(false, "This command only works during a run.");
        }

        Overclock overclock = player.GetRelic<Overclock>();
        if (overclock == null)
        {
            return new CmdResult(false, "The player does not have Overclock.");
        }

        if (!overclock.TryActivate(requestedLevel))
        {
            return new CmdResult(false, "Overclock cannot be activated right now.");
        }

        return new CmdResult(true, $"Overclock level {requestedLevel} activated.");
    }
}
