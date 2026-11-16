using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class ThirtyOneFCollection : TeamCollectionRelic
{
    public override string PackedIconPath => "res://images/relics/thirty_one_f.png";
    protected override int CountCards(Player player) => ThirtyOneFKeywordRules.GetPermanentDistinctCount(player);
    public override Task BeforeCombatStart()
    { ThirtyOneFBondHelper.ResetBattle(Owner); return Task.CompletedTask; }
    public override Task BeforeHandDraw(Player player, PlayerChoiceContext context, ICombatState combatState)
    {
        if (player == Owner) ThirtyOneFBondHelper.PrepareOpeningDraw(player);
        return Task.CompletedTask;
    }
    internal void AddDescriptionVariables(LocString text)
    {
        int count = IsMutable && Owner != null ? DisplayAmount : 0;
        text.Add("Kinds", count);
        text.Add("CounterDamage", HbrSkillPower.DamageBaseFromUnits(HbrSkillPower.DamageUnits(432m)));
        text.Add("ActiveTiers", count >= 6 ? "早已备好、每人22克、盾剑子弹" : count >= 4 ? "早已备好、每人22克" : count >= 2 ? "早已备好" : "无");
    }
}
