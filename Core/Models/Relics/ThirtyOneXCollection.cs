using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class ThirtyOneXCollection : TeamCollectionRelic
{
    public override string PackedIconPath => "res://images/relics/thirty_one_x.png";
    protected override int CountCards(Player player) => ThirtyOneXKeywordRules.GetPermanentDistinctCount(player);
    public override Task BeforeCombatStart() => ThirtyOneXBondHelper.Start(Owner);
    public override Task AfterCreatureAddedToCombat(Creature creature) => ThirtyOneXBondHelper.ApplyAura(creature?.CombatState, creature);
    public override decimal ModifyHandDraw(Player player, decimal count) => player == Owner
        ? count + ThirtyOneXBondHelper.OpeningDraw(player) : count;
    public override Task AfterCombatVictory(CombatRoom room) => ThirtyOneXBondHelper.Victory(Owner);
    public override Task AfterCombatEnd(CombatRoom room) { ThirtyOneXBondHelper.End(Owner); return Task.CompletedTask; }
    internal void AddDescriptionVariables(LocString text)
    {
        uint members = IsMutable ? ThirtyOneXBondHelper.Members(Owner) : 0;
        int tier = ThirtyOneXBondHelper.Tier(members);
        List<string> lines = new();
        if (tier > 0)
            for (int index = 0; index < ThirtyOneXKeywordRules.TotalKinds; index++)
                if (ThirtyOneXBondHelper.Has(members, (ThirtyOneXMember)index))
                {
                    LocString line = new("relics", Id.Entry + ".member." + index);
                    line.Add("Value", tier * (index is 0 or 2 ? 10 : index is 3 or 5 ? 5 : index == 4 ? 3 : 1));
                    lines.Add(line.GetFormattedText());
                }
        text.Add("Kinds", IsMutable ? DisplayAmount : 0);
        text.Add("Tier", tier);
        text.Add("MemberEffects", string.Join("\n", lines));
    }
}
