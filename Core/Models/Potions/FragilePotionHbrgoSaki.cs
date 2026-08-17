using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace HBRGOSakiMod.Core.Models.Potions;

public sealed class FragilePotionHbrgoSaki : ModPotionTemplate
{
    private static readonly PotionAssetProfile Assets = new(
        ImageHelper.GetImagePath("powers/fragile_power.png"));

    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyEnemy;

    public override PotionAssetProfile AssetProfile => Assets;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<FragilePower>(2m)
        };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<FragilePower>()
        };

    protected override async Task OnUse(
        PlayerChoiceContext choiceContext,
        Creature target)
    {
        AssertValidForTargetedPotion(target);
        await PowerCmd.Apply<FragilePower>(
            choiceContext,
            target,
            DynamicVars["FragilePower"].BaseValue,
            Owner.Creature,
            null);
    }
}
