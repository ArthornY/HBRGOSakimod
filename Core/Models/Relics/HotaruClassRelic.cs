using System.Collections.Generic;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Relics;

// Art is intentionally shared until the six original icons are supplied.
public abstract class HotaruClassRelic : RelicModel, IHotaruClassRelic
{
    public override string PackedIconPath => "res://images/relics/millsage.webp";
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => PackedIconPath;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => HotaruClassRelicCombatHelper.HoverTips(this);
}
