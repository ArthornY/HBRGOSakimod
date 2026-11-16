using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

// Selection-only model. It never enters a combat or permanent pile.
public sealed class YingxiaFormationChoiceHbrgoSaki : CardModel
{
    private WitnessMySupremacyHbrgoSaki _source;
    internal int Formation { get; private set; }
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedByModifiers => false;
    public override bool CanBeGeneratedInCombat => false;
    public override string PortraitPath => HbrMemoryGardenPortraits.For(this);
    public override string BetaPortraitPath => PortraitPath;
    public override string Title => new LocString("cards", "YINGXIA_FORMATION_CHOICE_HBRGO_SAKI.option." + Formation + ".title").GetFormattedText();
    public YingxiaFormationChoiceHbrgoSaki() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false) { }
    internal void Configure(WitnessMySupremacyHbrgoSaki source, int formation)
    { AssertMutable(); _source = source; Formation = formation; Owner = source.Owner; }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        LocString body = new("cards", "YINGXIA_FORMATION_CHOICE_HBRGO_SAKI.option." + Formation + ".description");
        YingxiaFormationCombatHelper.AddDescriptionParameters(_source, body);
        text.Add("FormationEffect", body);
    }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}
