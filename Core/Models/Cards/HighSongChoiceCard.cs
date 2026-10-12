using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
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
        LocString songDescription = new("cards", descriptionKey);
        DynamicVars.AddTo(songDescription);
        string energyPrefix = EnergyIconHelper.GetPrefix(this);
        songDescription.Add("energyPrefix", energyPrefix);
        string returnKey = this is LostDaysSongHbrgoSaki ? "returnDrawDescription" : "returnDiscardDescription";
        var returnDescription = new LocString("cards", "HIGH_SONG_HBRGO_SAKI." + returnKey);
        songDescription.Add("HighSongReturnDescription", returnDescription);
        // The description patch can select the level text directly instead of
        // the SongDescription wrapper, so both paths need the return text.
        description.Add("HighSongReturnDescription", returnDescription);
        foreach (object variable in songDescription.Variables.Values)
        {
            if (variable is EnergyVar energyVar)
            {
                energyVar.ColorPrefix = energyPrefix;
            }
        }

        description.Add(
            "SongDescription",
            songDescription);
    }

    protected HighSongChoiceCard(TargetType targetType)
        : base(0, CardType.Power, CardRarity.Token, targetType)
    {
    }

    protected abstract string GetTitleLocKey(int songLevel);

    protected Task ScheduleHighSongReturn(PileType pileType)
    {
        HighSongPower highSong = Owner.Creature.GetPower<HighSongPower>();
        highSong?.ScheduleCardReturn(pileType);
        return Task.CompletedTask;
    }
}
