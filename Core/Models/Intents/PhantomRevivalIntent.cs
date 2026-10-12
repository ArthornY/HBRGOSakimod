using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace HBRGOSakiMod.Core.Models.Intents;

// NIntent renders numeric labels for StatusIntent as well as attacks. This is
// presentation only: it is never installed in a monster's move state machine.
internal sealed class PhantomRevivalIntent : StatusIntent
{
    private readonly MomentOfReign _relic;

    internal PhantomRevivalIntent(MomentOfReign relic) : base(0) => _relic = relic;

    public override IntentType IntentType => IntentType.Heal;
    protected override string IntentPrefix => "HBRGO_PHANTOM_REVIVAL";
    protected override string SpritePath => "atlases/intent_atlas.sprites/intent_heal.tres";

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner) => "heal";

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        var text = new LocString("intents", "HBRGO_PHANTOM_REVIVAL.label");
        text.Add("Turns", _relic.GetPhantomReviveTurnsRemaining() ?? 0);
        return text;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        var text = new LocString("intents", "HBRGO_PHANTOM_REVIVAL.description");
        text.Add("Turns", _relic.GetPhantomReviveTurnsRemaining() ?? 0);
        return text;
    }
}
