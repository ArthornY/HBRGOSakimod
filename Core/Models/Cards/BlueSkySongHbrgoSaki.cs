using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class BlueSkySongHbrgoSaki : HighSongChoiceCard
{
    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/blue_sky_song_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public BlueSkySongHbrgoSaki()
        : base(TargetType.Self)
    {
    }

    protected override string GetTitleLocKey(int songLevel)
    {
        return songLevel switch
        {
            1 => "BLUE_SKY_SONG_HBRGO_SAKI.level1Title",
            2 => "BLUE_SKY_SONG_HBRGO_SAKI.level2Title",
            _ => "BLUE_SKY_SONG_HBRGO_SAKI.level3Title"
        };
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int songLevel = SongLevel;
        int highSongAmount = HighSongAmount;
        int reductionPerLayer = songLevel >= 3 ? 20 : 10;
        int counterBonus = songLevel switch
        {
            2 => highSongAmount,
            >= 3 => highSongAmount * 2,
            _ => 0
        };

        foreach (Creature ally in TeamCombatHelper.GetLivingPlayerCreatures(Owner.Creature))
        {
            BlueSkyPower blueSky = await PowerCmd.Apply<BlueSkyPower>(
                choiceContext,
                ally,
                highSongAmount * reductionPerLayer,
                Owner.Creature,
                this);
            blueSky?.AddCounterDamageBonus(counterBonus);

            if (songLevel >= 3)
            {
                BlueSkyGuardPower guard = await PowerCmd.Apply<BlueSkyGuardPower>(
                    choiceContext,
                    ally,
                    1m,
                    Owner.Creature,
                    this);
                guard?.RestoreGuard();
            }
        }

        if (songLevel >= 3 && !CombatModeHelper.HasEnemyAttackIntent(Owner.Creature))
        {
            decimal strikeBaseDamage =
                ModelDb.Card<StrikeHbrgoSaki>().DynamicVars.Damage.BaseValue;
            decimal damage = strikeBaseDamage * 2m + songLevel * 2m;
            await GeneratedStrikeHelper.PlayAll(
                choiceContext,
                Owner,
                damage);
        }

        ScheduleHighSongReturn(PileType.Discard);
    }
}
