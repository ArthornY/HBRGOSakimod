using System;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Integration;

internal static class CardExhaustRuntimeCompatibility
{
    private static readonly Func<PlayerChoiceContext, CardModel, bool, bool, Task> ExhaustCard = ResolveExhaust();

    internal static Task Exhaust(PlayerChoiceContext choiceContext, CardModel card,
        bool causedByEthereal = false, bool skipVisuals = false) =>
        ExhaustCard(choiceContext, card, causedByEthereal, skipVisuals);

    private static Func<PlayerChoiceContext, CardModel, bool, bool, Task> ResolveExhaust()
    {
        MethodInfo method = typeof(CardCmd).GetMethod(nameof(CardCmd.Exhaust),
            BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool), typeof(bool) }, null);
        if (method == null || !typeof(Task).IsAssignableFrom(method.ReturnType))
        {
            throw new MissingMethodException(
                $"HBRGOSakiMod: no compatible CardCmd.Exhaust(PlayerChoiceContext, CardModel, bool, bool) "
                + $"returning Task in {typeof(CardCmd).Assembly.FullName}.");
        }

        // Public beta returns Task<CardPileAddResult?> instead of Task. A covariant
        // delegate accepts either without a binary reference to the old return type,
        // and preserves the original task's completion, cancellation and exceptions.
        return method.CreateDelegate<Func<PlayerChoiceContext, CardModel, bool, bool, Task>>();
    }
}
