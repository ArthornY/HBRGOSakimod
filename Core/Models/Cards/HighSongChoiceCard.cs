using System;
using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public abstract class HighSongChoiceCard : CardModel
{
    public int HighSongAmount
    {
        get
        {
            Creature ownerCreature = IsMutable ? Owner?.Creature : null;
            return Math.Max(
                1,
                ownerCreature?.GetPower<HighSongPower>()?.Amount ??
                CurrentUpgradeLevel + 1);
        }
    }

    public int SongLevel
    {
        get
        {
            return Math.Clamp(HighSongAmount, 1, 3);
        }
    }

    public override int MaxUpgradeLevel => 2;

    public override string Title =>
        new LocString("cards", GetTitleLocKey(SongLevel)).GetFormattedText();

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Retain
    };

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string descriptionKey = GetTitleLocKey(SongLevel)
            .Replace("Title", "Description", StringComparison.Ordinal);
        description.Add(
            "SongDescription",
            new LocString("cards", descriptionKey));
    }

    protected HighSongChoiceCard(TargetType targetType)
        : base(0, CardType.Power, CardRarity.Token, targetType)
    {
    }

    protected abstract string GetTitleLocKey(int songLevel);

    protected void ScheduleHighSongReturn(PileType pileType)
    {
        Owner.Creature.GetPower<HighSongPower>()?.ScheduleCardReturn(pileType);
    }
}
