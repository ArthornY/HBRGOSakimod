using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Screens;
using STS2RitsuLib.Interactions.RightClick;

namespace HBRGOSakiMod.Core.Integration;

internal static class HBRGOSakiRightClickIntegration
{
    private static bool _registered;

    internal static void Register()
    {
        if (_registered)
        {
            return;
        }

        ModRightClickRegistry.Register(LocalRelicScreenHandler.Instance);
        _registered = true;
    }

    private sealed class LocalRelicScreenHandler : IModRightClickHandler
    {
        internal static readonly LocalRelicScreenHandler Instance = new();

        public int Priority => 100;

        public bool TryHandle(ModRightClickContext context)
        {
            switch (context.Model)
            {
                case ElectronicSoldierManual manual:
                    NSixSenseStatusScreen.Show(manual);
                    return true;
                case SoulOfAnchor soulOfAnchor:
                    NSoulRollHistoryScreen.Show(soulOfAnchor);
                    return true;
                default:
                    return false;
            }
        }
    }
}
