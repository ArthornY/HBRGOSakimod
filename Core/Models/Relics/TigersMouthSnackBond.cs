using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class TigersMouthSnackBond : TeamCollectionRelic, IActiveDuoBond
{
    internal sealed record FoodRelic(string Id, string Name);
    // This is the single whitelist and display-name source for the food rule.
    internal static IReadOnlyList<FoodRelic> Foods { get; } = Array.AsReadOnly(new[]
    {
        new FoodRelic("STRAWBERRY", "草莓"), new("PEAR", "梨子"), new("MANGO", "芒果"),
        new("FAKE_MANGO", "芒果？？？"), new("DRAGON_FRUIT", "火龙果"),
        new("LEES_WAFFLE", "李家华夫饼"), new("FAKE_LEES_WAFFLE", "李家华夫饼？？？"),
        new("CHOSEN_CHEESE", "天选芝士"), new("BREAD", "面包"), new("YUMMY_COOKIE", "美味饼干"),
        new("ICE_CREAM", "冰淇淋"), new("LASTING_CANDY", "吃不完的糖"), new("LUNAR_PASTRY", "月亮糕点"),
        new("MEAT_ON_THE_BONE", "带骨肉"), new("NUTRITIOUS_SOUP", "营养汤"),
        new("BONE_TEA", "骨茶"), new("EMBER_TEA", "余烬茶"), new("RED_VINE_TEA", "赤藤茶"),
        new("TEA_OF_DISCOURTESY", "无礼之茶"), new("VERY_HOT_COCOA", "烫嘴可可")
    });
    private static readonly HashSet<string> FoodIds = new(Foods.Select(food => food.Id), StringComparer.Ordinal);

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RanaFoodBonuses { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ByakkoFoodBonuses { get; private set; }

    public bool IsBondActive => IsActive;
    internal bool IsActive => !IsMelted && Owner != null && DisplayAmount >= 2 && HbrBattleStatsHelper.IsEnabled(Owner);
    public override string PackedIconPath => ImageHelper.GetImagePath("relics/thirty_a.png");
    protected override int CountCards(Player player) => ThirtyOneBBondHelper.SnackKinds(player);
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            LocString text = new("relics", "TIGERS_MOUTH_SNACK_BOND.foodDescription");
            text.Add("FoodList", string.Join("、", Foods.Select(food => food.Name)));
            yield return new HoverTip(new LocString("relics", "TIGERS_MOUTH_SNACK_BOND.foodTitle"), text);
        }
    }

    internal static bool IsFood(RelicModel relic) => relic != null && FoodIds.Contains(relic.Id.Entry);

    internal void GrantFoodBonus()
    {
        AssertMutable();
        if (Owner.RunState.Rng.Niche.NextInt(2) == 0)
            RanaFoodBonuses = (int)Math.Min(int.MaxValue, (long)RanaFoodBonuses + 1L);
        else ByakkoFoodBonuses = (int)Math.Min(int.MaxValue, (long)ByakkoFoodBonuses + 1L);
        Flash();
        InvokeDisplayAmountChanged();
    }

    internal static decimal GetDamageBonusUnits(Creature dealer, CardModel cardSource)
    {
        if (cardSource is not (MatchaParfaitHbrgoSaki or ByakkoRoarHbrgoSaki) ||
            cardSource.Owner?.Creature != dealer || !HbrBattleStatsHelper.IsEnabled(dealer) ||
            cardSource.Owner.Relics.OfType<TigersMouthSnackBond>().FirstOrDefault() is not { IsMelted: false } bond) return 0m;
        // The reward is permanent once earned, even if one partner is removed.
        int count = cardSource is MatchaParfaitHbrgoSaki ? bond.RanaFoodBonuses : bond.ByakkoFoodBonuses;
        return HbrSkillPower.DamageUnits(count * 10m);
    }

    internal void AddDescriptionVariables(LocString text)
    {
        text.Add("ActiveText", IsMutable && IsActive ? "已生效" : "未生效");
        text.Add("RanaBonus", (RanaFoodBonuses * 10m).ToString("0.#", CultureInfo.InvariantCulture));
        text.Add("ByakkoBonus", (ByakkoFoodBonuses * 10m).ToString("0.#", CultureInfo.InvariantCulture));
    }
}
