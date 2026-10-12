using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomKnowledgeIntentHelper
{
    private static readonly Func<KnowledgeDemon, int> GetCounter =
        AccessTools.MethodDelegate<Func<KnowledgeDemon, int>>(
            AccessTools.PropertyGetter(typeof(KnowledgeDemon), "CurseOfKnowledgeCounter"));
    private static readonly Action<KnowledgeDemon, int> SetCounter =
        AccessTools.MethodDelegate<Action<KnowledgeDemon, int>>(
            AccessTools.PropertySetter(typeof(KnowledgeDemon), "CurseOfKnowledgeCounter"));

    internal static async Task<bool> TryPerform(Creature phantom, MoveState move,
        IReadOnlyList<Creature> targets)
    {
        if (phantom.Monster is not KnowledgeDemon demon ||
            move.Id != "CURSE_OF_KNOWLEDGE_MOVE" ||
            PhantomPlayerBenefitContext.CurrentPhantom != phantom)
            return false;

        var chooser = phantom.PetOwner;
        int counter = Math.Clamp(GetCounter(demon), 0, 2);
        int damage = 6 + counter;
        if (chooser?.PlayerCombatState != null && targets.Count > 0)
        {
            List<PhantomKnowledgeChoiceHbrgoSaki> choices = new(2);
            try
            {
                var curse = phantom.CombatState.CreateCard<PhantomKnowledgeChoiceHbrgoSaki>(chooser);
                choices.Add(curse);
                curse.Configure(0, damage);
                var benefit = phantom.CombatState.CreateCard<PhantomKnowledgeChoiceHbrgoSaki>(chooser);
                choices.Add(benefit);
                benefit.Configure(counter + 1, damage);
                CardModel selected = await CardSelectCmd.FromChooseACardScreen(
                    new BlockingPlayerChoiceContext(), choices, chooser);
                if (selected is PhantomKnowledgeChoiceHbrgoSaki choice)
                {
                    if (choice.ChoiceIndex == 0)
                        await PowerCmd.Apply<DisintegrationPower>(new ThrowingPlayerChoiceContext(),
                            targets.Where(PhantomHelper.IsValidPhantomTarget), damage, phantom, null);
                    else if (choice.ChoiceIndex == 1)
                        PhantomPlayerBenefitContext.QueueDrawPerTurn(1);
                    else if (choice.ChoiceIndex == 2)
                        PhantomPlayerBenefitContext.QueueHandLimit(1);
                    else
                        PhantomPlayerBenefitContext.QueueMaxEnergy(1);
                }
            }
            finally
            {
                foreach (PhantomKnowledgeChoiceHbrgoSaki choice in choices)
                    choice.RemoveFromState();
            }
        }

        SetCounter(demon, GetCounter(demon) + 1);
        return true;
    }
}
