using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace HBRGOSakiMod.Core.Models.Potions;

public sealed class DpPotionHbrgoSaki : ModPotionTemplate
{
    private const string DpVar = "Dp";

    private static readonly PotionAssetProfile Assets = new(
        ImageHelper.GetImagePath("relics/deflector_protection.png"));

    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.Self;

    public override PotionAssetProfile AssetProfile => Assets;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar(DpVar, 20m)
        };

    protected override Task OnUse(
        PlayerChoiceContext choiceContext,
        Creature target)
    {
        Owner.GetRelic<DeflectorProtection>()?.RestoreDp(
            (int)DynamicVars[DpVar].BaseValue);
        return Task.CompletedTask;
    }
}
