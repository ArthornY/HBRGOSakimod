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

public sealed class SoulPotionHbrgoSaki : ModPotionTemplate
{
    private const string SoulVar = "Soul";

    private static readonly PotionAssetProfile Assets = new(
        ImageHelper.GetImagePath("relics/restoration_moment.png"));

    public override PotionRarity Rarity => PotionRarity.None;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.Self;

    public override PotionAssetProfile AssetProfile => Assets;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar(SoulVar, 15m)
        };

    protected override Task OnUse(
        PlayerChoiceContext choiceContext,
        Creature target)
    {
        Owner.GetRelic<RestorationMoment>()?.GainCounter(
            (int)DynamicVars[SoulVar].BaseValue);
        return Task.CompletedTask;
    }
}
