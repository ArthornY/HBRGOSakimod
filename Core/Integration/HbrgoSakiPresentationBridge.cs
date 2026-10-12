using System;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Integration;

public interface IHbrgoSakiPresentationProvider
{
    void OnRollResolved(Player player, int roll);

    float? PlayOverclockVoice(string voiceId);

    void StopOverclockVoice();
}

public static class HbrgoSakiPresentationBridge
{
    private static IHbrgoSakiPresentationProvider _provider;

    public static void Register(IHbrgoSakiPresentationProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    internal static void NotifyRollResolved(Player player, int roll)
    {
        _provider?.OnRollResolved(player, roll);
    }

    internal static float? PlayOverclockVoice(string voiceId)
    {
        return _provider?.PlayOverclockVoice(voiceId);
    }

    internal static void StopOverclockVoice()
    {
        _provider?.StopOverclockVoice();
    }
}

