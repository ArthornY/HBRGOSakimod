namespace HBRGOSakiMod.Core.Helpers;

internal sealed record HotaruCannonUpgradeChoice(
    HotaruCannonKind Kind,
    string Name,
    string IconPath,
    string CurrentDescription,
    string UpgradeDescription,
    bool IsAvailable,
    string StatusText);
