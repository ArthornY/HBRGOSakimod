using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class PhantomKnowledgeChoiceHbrgoSaki : CardModel
{
    [SavedProperty] public int ChoiceIndex { get; private set; }
    [SavedProperty] public string ChoiceTitle { get; private set; } = string.Empty;
    [SavedProperty] public string ChoiceDescription { get; private set; } = string.Empty;

    public override string Title => string.IsNullOrEmpty(ChoiceTitle) ? base.Title : ChoiceTitle;
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override int MaxUpgradeLevel => 0;

    public PhantomKnowledgeChoiceHbrgoSaki() : base(-1, CardType.Skill,
        CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false) { }

    internal void Configure(int choiceIndex, int damage)
    {
        AssertMutable();
        ChoiceIndex = choiceIndex;
        ChoiceTitle = choiceIndex switch
        {
            0 => "瓦解", 1 => "知识启迪", 2 => "自由思考", _ => "充沛精神"
        };
        ChoiceDescription = choiceIndex switch
        {
            0 => $"使敌方全体获得{damage}层[gold]瓦解[/gold]。每层在其回合结束时造成1点伤害。",
            1 => "本场战斗，受益玩家每回合多抽1张牌。",
            2 => "本场战斗，受益玩家的手牌上限增加1。",
            _ => "本场战斗，受益玩家的最大能量增加[E]。"
        };
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("CardDescription", ChoiceDescription);
    }
}
