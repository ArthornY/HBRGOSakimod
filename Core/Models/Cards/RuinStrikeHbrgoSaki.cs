using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class RuinStrikeHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/ruin_strike_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(4m, ValueProp.Move), new ResistanceHitCountVar()
    };

    public RuinStrikeHbrgoSaki() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target).WithHitCount(GetHitCount(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);

    internal static int GetHitCount(CardModel card)
    {
        // Snapshot the breaks already recorded when this play starts. This card's
        // own resistance reduction resolves afterward, in AfterCardPlayedLate.
        int breaks = card?.IsMutable == true
            ? SixSenseCombatHelper.ResistanceBreakCount(card.Owner)
            : 0;
        return 1 + Math.Clamp(breaks, 0, int.MaxValue - 1);
    }

    private sealed class ResistanceHitCountVar : RepeatVar
    {
        internal ResistanceHitCountVar() : base(1) { }

        public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode,
            Creature target, bool runGlobalHooks) => PreviewValue = GetHitCount(card);
    }
}
