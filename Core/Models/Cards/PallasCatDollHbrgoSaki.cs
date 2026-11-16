using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class PallasCatDollHbrgoSaki : CardModel
{
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedByModifiers => false;
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain, CardKeyword.Exhaust };
    public PallasCatDollHbrgoSaki() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false) { }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => HbrBattleStatsHelper.IsEnabled(Owner)
        ? PlayerCmd.GainEnergy(1m, Owner) : Task.CompletedTask;
}
