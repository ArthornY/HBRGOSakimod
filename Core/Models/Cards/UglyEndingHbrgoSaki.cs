using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class UglyEndingHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/ugly_ending_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable
    {
        get
        {
            Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
            return phantom != null && CombatState.Allies.Any(creature =>
                creature.IsAlive &&
                PhantomHelper.IsMinionOf(phantom, creature));
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new DamageVar(9m, ValueProp.Move),
            new IntVar("PermanentDamageBonus", 3m),
            new IntVar("Experience", 3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<PhantomPower>()
        };

    public UglyEndingHbrgoSaki()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom == null)
        {
            return;
        }

        MomentOfReign relic = Owner.GetRelic<MomentOfReign>();
        int rosterEntryId = relic?.ActiveRosterEntryId ?? 0;
        Creature[] minions = CombatState.Allies
            .Where(creature =>
                creature.IsAlive &&
                PhantomHelper.IsMinionOf(phantom, creature))
            .ToArray();
        int killed = 0;
        foreach (Creature minion in minions)
        {
            if (!minion.IsAlive)
            {
                continue;
            }

            await CreatureCmd.Kill(minion, force: true);
            if (minion.IsDead)
            {
                killed++;
            }
        }

        relic?.AddMinionSacrificeGrowth(rosterEntryId, killed,
            DynamicVars["PermanentDamageBonus"].BaseValue,
            DynamicVars["Experience"].IntValue);
        for (int i = 0; i < killed; i++)
        {
            if (CombatManager.Instance.IsOverOrEnding || !CombatState.HittableEnemies.Any())
            {
                break;
            }

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCardCompat(this, cardPlay)
                .TargetingAllOpponents(CombatState)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
