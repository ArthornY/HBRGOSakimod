using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class AngryIzumiYukiTemporaryStrengthPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<EhehePeroroHbrgoSaki>();

    public override LocString Title => new("powers", Id.Entry + ".title");
}
