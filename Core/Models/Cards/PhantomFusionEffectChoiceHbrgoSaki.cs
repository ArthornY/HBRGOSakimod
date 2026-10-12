using System;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class PhantomFusionEffectChoiceHbrgoSaki : CardModel
{
    [SavedProperty]
    public int ReplacementEffectTypeValue { get; private set; }

    [SavedProperty]
    public string ChoiceTitle { get; private set; } = string.Empty;

    [SavedProperty]
    public string ChoiceDescription { get; private set; } = string.Empty;

    internal PhantomFusionEffectType ReplacementEffectType =>
        Enum.IsDefined(
            typeof(PhantomFusionEffectType),
            ReplacementEffectTypeValue)
            ? (PhantomFusionEffectType)ReplacementEffectTypeValue
            : PhantomFusionEffectType.None;

    public override string Title => string.IsNullOrWhiteSpace(ChoiceTitle)
        ? base.Title
        : ChoiceTitle;

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/phantom_fusion_effect_choice_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    public PhantomFusionEffectChoiceHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    internal void ConfigureSkip(string incomingDescription)
    {
        AssertMutable();
        ReplacementEffectTypeValue = (int)PhantomFusionEffectType.None;
        ChoiceTitle = "不应用新效果";
        ChoiceDescription =
            "保留现有强化效果，不应用本次新增效果：\n" +
            incomingDescription;
    }

    internal void ConfigureReplacement(
        PhantomFusionEffectType replacementType,
        string currentDescription,
        string incomingDescription)
    {
        AssertMutable();
        ReplacementEffectTypeValue = (int)replacementType;
        ChoiceTitle = "替换此效果";
        ChoiceDescription =
            "移除：\n" + currentDescription +
            "\n\n应用：\n" + incomingDescription;
    }

    internal void ConfigureRemoval(
        PhantomFusionEffectType replacementType,
        string currentDescription)
    {
        AssertMutable();
        ReplacementEffectTypeValue = (int)replacementType;
        ChoiceTitle = "移除此效果";
        ChoiceDescription =
            "已有强化效果超过5种。移除：\n" +
            currentDescription;
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("CardDescription", ChoiceDescription);
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay) => Task.CompletedTask;

    protected override void OnUpgrade()
    {
    }
}
