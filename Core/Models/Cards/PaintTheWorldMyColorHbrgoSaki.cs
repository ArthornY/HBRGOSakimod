using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class PaintTheWorldMyColorHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneDMemberCard
{
    internal const int Flash = 0, Saturation = 1, Spectacle = 2, ColorlessBullet = 3,
        UltimateColor = 4, Impasto = 5, Tombstone = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyOneDMember ThirtyOneDMember => ThirtyOneDMember.IshiiIroha;
    public PaintTheWorldMyColorHbrgoSaki() : base(1, CardType.Skill, TargetType.Self) { }
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { IrohaDyeState.HoverTip(), HoverTipFactory.FromPower<HbrAttackUpPower>(),
      HoverTipFactory.FromPower<HbrCriticalRateUpPower>(), HoverTipFactory.FromPower<HbrCriticalDamageUpPower>(),
      HoverTipFactory.FromPower<MinionPower>() }
        .Append(HoverTipCompatibility.CreateRelicReference<HBRGOSakiMod.Core.Models.Relics.Overclock>());
    protected override void AddExtraArgsToDescription(LocString text)
    { base.AddExtraArgsToDescription(text); text.Add("DyePercent", HasUpgradeEffect(Spectacle) ? 80 : 50); }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (IrohaDyeCombatHelper.MarkPlayed(this) && HasUpgradeEffect(ColorlessBullet))
            Owner.GetRelic<HBRGOSakiMod.Core.Models.Relics.Overclock>()?.GainLevelProgress(30m);
        CardModel[] targets = Owner.PlayerCombatState.Hand.Cards.Where(card => card.Type == CardType.Attack).ToArray();
        decimal percent = HasUpgradeEffect(Spectacle) ? 80m : 50m;
        if (targets.Length == 0) { IrohaDyeCombatHelper.PromiseDraw(Owner, percent); return; }
        if (!HasUpgradeEffect(Saturation))
            targets = (await CardSelectCmd.FromHand(context, Owner,
                new CardSelectorPrefs(new LocString("cards", Id.Entry + ".selectionPrompt"), 1),
                card => card.Type == CardType.Attack, this)).Where(card => card.Owner == Owner &&
                card.Pile?.Type == PileType.Hand && card.Type == CardType.Attack).Take(1).ToArray();
        foreach (CardModel target in targets) IrohaDyeCombatHelper.Paint(target, percent);
    }
}
