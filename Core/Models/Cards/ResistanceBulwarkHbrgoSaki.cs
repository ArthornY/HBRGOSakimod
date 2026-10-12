using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ResistanceBulwarkHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(3m, ValueProp.Move), new ResistanceBlockCountVar()
    };

    public ResistanceBulwarkHbrgoSaki() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // Keep the number of separate block gains fixed for this play. The
        // card's own attribute reduction is recorded after its effects finish.
        int repeats = GetBlockCount(this);
        for (int i = 0; i < repeats && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding; i++)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);

    internal static int GetBlockCount(CardModel card)
    {
        int breaks = card?.IsMutable == true
            ? SixSenseCombatHelper.ResistanceBreakCount(card.Owner)
            : 0;
        return 1 + Math.Clamp(breaks, 0, int.MaxValue - 1);
    }

    private sealed class ResistanceBlockCountVar : RepeatVar
    {
        internal ResistanceBlockCountVar() : base(1) { }

        public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode,
            Creature target, bool runGlobalHooks) => PreviewValue = GetBlockCount(card);
    }
}
