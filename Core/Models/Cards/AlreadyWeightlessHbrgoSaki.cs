using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class AlreadyWeightlessHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override bool IsPlayable =>
        PhantomHelper.GetLivingPhantom(Owner) != null &&
        Owner.GetRelic<MomentOfReign>()?
            .GetConsumableRosterEntries().Count > 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("ExperienceMultiplier", 3m),
            new IntVar("DamageBonusPercent", 0m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public AlreadyWeightlessHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        MomentOfReign relic = Owner.GetRelic<MomentOfReign>();
        IReadOnlyList<PhantomRosterEntry> entries =
            relic?.GetConsumableRosterEntries();
        if (relic == null || entries == null || entries.Count == 0)
        {
            return;
        }

        List<PhantomChoiceHbrgoSaki> choices = entries
            .Select(entry =>
            {
                PhantomChoiceHbrgoSaki choice = CombatState
                    .CreateCard<PhantomChoiceHbrgoSaki>(Owner);
                choice.SetRosterEntry(entry);
                return choice;
            })
            .OrderByPhantomGrowth()
            .ToList();
        try
        {
            PhantomChoiceHbrgoSaki selected =
                (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    choices,
                    Owner,
                    new CardSelectorPrefs(
                        new LocString(
                            "cards",
                            Id.Entry + ".selectionScreenPrompt"),
                        1)))
                .OfType<PhantomChoiceHbrgoSaki>()
                .FirstOrDefault();
            if (selected != null)
            {
                await relic.TryConsumeRosterEntryForActivePhantom(
                    choiceContext,
                    selected.RosterEntry.InstanceId,
                    DynamicVars["ExperienceMultiplier"].IntValue,
                    DynamicVars["DamageBonusPercent"].BaseValue);
            }
        }
        finally
        {
            foreach (PhantomChoiceHbrgoSaki choice in choices)
            {
                choice.RemoveFromState();
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DamageBonusPercent"].UpgradeValueBy(2m);
    }
}
