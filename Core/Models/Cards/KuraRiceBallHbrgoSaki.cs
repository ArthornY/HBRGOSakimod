using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class KuraRiceBallHbrgoSaki : CardModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)] public bool ForWholeTeam { get; internal set; }
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override TargetType TargetType => ForWholeTeam ? TargetType.AllAllies : IsMutable &&
        Owner?.Creature?.CombatState?.Players.Count(player => player.Creature.IsAlive) <= 1 ? TargetType.Self : TargetType.AnyAlly;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<HbrMoralePower>() };
    public KuraRiceBallHbrgoSaki() : base(0, CardType.Skill, CardRarity.Token, TargetType.AnyAlly, shouldShowInCardLibrary: false) { }
    protected override void AddExtraArgsToDescription(LocString text)
    { text.Add("RiceRecipients", ForWholeTeam ? "我方全体各" : "一名我方单位"); }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var allies = HbrPlayerTeamTargets.Get(Owner.Creature);
        var recipients = ForWholeTeam ? allies : new[]
        { TargetType == TargetType.Self ? Owner.Creature : play.Target is { } selected && allies.Contains(selected) ? selected : Owner.Creature };
        foreach (var ally in recipients)
            await PowerCmd.Apply<HbrMoralePower>(context, ally, 1m, Owner.Creature, null);
    }
}
