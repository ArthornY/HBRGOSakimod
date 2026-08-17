using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class RedCrimsonExtraStrikeHbrgoSaki : CardModel
{
    private const string DefaultPortrait =
        "packed/card_portraits/hbrgo_saki/red_crimson_extra_strike_hbrgo_saki.png";
    private const string BigDogChewPortrait =
        "packed/card_portraits/hbrgo_saki/red_crimson_extra_strike_hbrgo_saki_meme.png";

    public override string PortraitPath => ImageHelper.GetImagePath(
        HBRGOSakiConfig.UseBigDogChewCardFace
            ? BigDogChewPortrait
            : DefaultPortrait);

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<string> AllPortraitPaths =>
        new[]
        {
            ImageHelper.GetImagePath(DefaultPortrait),
            ImageHelper.GetImagePath(BigDogChewPortrait)
        };

    public override string Title
    {
        get
        {
            if (!HBRGOSakiConfig.UseBigDogChewCardFace)
            {
                return base.Title;
            }

            string title = new LocString(
                "cards",
                "RED_CRIMSON_EXTRA_STRIKE_HBRGO_SAKI.bigDogChewTitle")
                .GetFormattedText();
            if (!IsUpgraded)
            {
                return title;
            }

            return MaxUpgradeLevel > 1
                ? $"{title}+{CurrentUpgradeLevel}"
                : title + "+";
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new RepeatVar(2)
        };

    public RedCrimsonExtraStrikeHbrgoSaki()
        : base(
            2,
            CardType.Skill,
            CardRarity.Uncommon,
            TargetType.Self)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        RedCrimson redCrimson =
            Owner.Creature.CombatState?.Allies
                .Where(creature =>
                    creature.IsAlive &&
                    creature.PetOwner == Owner)
                .Select(creature => creature.Monster)
                .OfType<RedCrimson>()
                .FirstOrDefault();
        redCrimson?.EmpowerNextAttack(
            (int)DynamicVars.Repeat.BaseValue);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}
