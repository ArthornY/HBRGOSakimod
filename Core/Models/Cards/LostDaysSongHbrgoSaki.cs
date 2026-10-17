using System;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class LostDaysSongHbrgoSaki : HighSongChoiceCard
{
    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/lost_days_song_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public LostDaysSongHbrgoSaki()
        : base(TargetType.Self)
    {
    }

    protected override string GetTitleLocKey(int songLevel)
    {
        return songLevel switch
        {
            1 => "LOST_DAYS_SONG_HBRGO_SAKI.level1Title",
            2 => "LOST_DAYS_SONG_HBRGO_SAKI.level2Title",
            _ => "LOST_DAYS_SONG_HBRGO_SAKI.level3Title"
        };
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int songLevel = SongLevel;
        HighSongPower highSong = Owner.Creature.GetPower<HighSongPower>();
        if (songLevel >= 3 && highSong != null)
        {
            long teamCriticalSuccesses = Owner.Creature.CombatState.Players
                .Sum(player => (long)RollKeywordCombatState.GetCriticalSuccessCount(player));
            int highSongGain = (int)Math.Min(teamCriticalSuccesses,
                Math.Max(0, HighSongPower.GetMaximumAmount(Owner.Creature) - highSong.Amount));
            if (highSongGain > 0)
            {
                await PowerCmd.ModifyAmount(
                    choiceContext,
                    highSong,
                    highSongGain,
                    Owner.Creature,
                    this);
            }
        }

        int chargeAmount = highSong?.Amount ?? HighSongAmount;
        foreach (Creature ally in TeamCombatHelper.GetLivingAllies(Owner.Creature))
        {
            await PowerCmd.Apply<ChargePower>(
                choiceContext,
                ally,
                chargeAmount,
                Owner.Creature,
                this);

            if (songLevel >= 2)
            {
                await PowerCmd.Apply<TinyMomentsPower>(
                    choiceContext,
                    ally,
                    1m,
                    Owner.Creature,
                    this);
            }

            if (songLevel >= 3)
            {
                await PowerCmd.Apply<LostMelodyPower>(
                    choiceContext,
                    ally,
                    1m,
                    Owner.Creature,
                    this);
            }
        }

        await ScheduleHighSongReturn(PileType.Draw);
    }
}
