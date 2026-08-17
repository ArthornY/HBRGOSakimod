using BaseLib.Config;

namespace HBRGOSakiMod.Core.Config;

public sealed class HBRGOSakiConfig : SimpleModConfig
{
    [ConfigHoverTip]
    public static bool UseBigDogChewCardFace { get; set; } = false;
}
