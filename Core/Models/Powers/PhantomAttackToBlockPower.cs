using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PhantomAttackToBlockPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BonusBlock { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        if (cardSource is PuttingOnAirsHbrgoSaki
            {
                IsUpgraded: true
            })
        {
            AssertMutable();
            BonusBlock += PuttingOnAirsHbrgoSaki.UpgradedBonusBlock;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player == Owner.PetOwner)
        {
            await PowerCmd.Remove(this);
        }
    }
}
