using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PhantomNoExhaustPower : PowerModel, ICustomPower, IHbrAfterDeathListener
{
    public string CustomPackedIconPath => ModelDb.Power<FeelNoPainPower>().IconPath;
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords) =>
        Owner.Player != null && card.Owner == Owner.Player &&
        card.CombatState == Owner.CombatState && keywords.Remove(CardKeyword.Exhaust);

    public async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Applier)
            await PowerCmd.Remove(this);
    }
}
