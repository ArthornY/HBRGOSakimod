using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Explicit audited resource-payment callsites issue a one-command intent.
// Unpowered damage, self targeting and card ownership alone are not costs.
internal static class HbrHealthCostContext
{
    internal sealed class Intent : IDisposable
    {
        internal Intent Previous;
        internal Creature Target, Dealer;
        internal CardModel Card;
        internal decimal Amount;
        internal ValueProp Props;
        internal bool Active = true, Claimed;
        public void Dispose()
        { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly AsyncLocal<Intent> Current = new();
    internal static Intent Begin(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel card)
    {
        Intent intent = new() { Previous = Current.Value, Target = target, Amount = amount, Props = props, Dealer = dealer, Card = card };
        Current.Value = intent;
        return intent;
    }
    internal static bool TryClaim(ref IEnumerable<Creature> targets, decimal amount, ValueProp props, Creature dealer, CardModel card)
    {
        Intent intent = Current.Value;
        if (intent?.Active != true || intent.Claimed || intent.Target == null || intent.Amount != amount || intent.Props != props ||
            !ReferenceEquals(intent.Dealer, dealer) || !ReferenceEquals(intent.Card, card)) return false;
        if (targets is not IReadOnlyList<Creature> list) targets = list = targets?.ToArray();
        if (list?.Count != 1 || !ReferenceEquals(list[0], intent.Target)) return false;
        intent.Claimed = true;
        return true;
    }
}
