using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class WorldThatWillComeHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar("RequiredActions", 40m)
        };

    internal int RemainingActions => Math.Max(0,
        DynamicVars["RequiredActions"].IntValue -
        PhantomHelper.GetActionsPerformed(IsMutable ? Owner : null));

    protected override bool IsPlayable => RemainingActions == 0;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("RemainingActions", RemainingActions);
    }

    internal void RefreshActionProgress()
    {
        if (IsMutable && Pile?.Type == PileType.Hand)
        {
            NCard.FindOnTable(this)?.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
        }
    }

    public WorldThatWillComeHbrgoSaki()
        : base(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        var enemies = CombatState.Enemies
            .Where(enemy => enemy.IsAlive)
            .ToArray();
        foreach (var enemy in enemies)
        {
            foreach (var power in enemy.Powers.ToArray())
            {
                await PowerCmd.Remove(power);
            }
        }

        await CreatureCmd.Kill(enemies, force: true);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["RequiredActions"].UpgradeValueBy(-10m);
    }
}
