using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ShadowDanceSongHbrgoSaki : HighSongChoiceCard
{
    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/shadow_dance_song_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new EnergyVar(2)
        };

    public ShadowDanceSongHbrgoSaki()
        : base(HBRGOSakiTargetTypes.FriendlyPlayerOrPhantom)
    {
    }

    protected override string GetTitleLocKey(int songLevel)
    {
        return songLevel switch
        {
            1 => "SHADOW_DANCE_SONG_HBRGO_SAKI.level1Title",
            2 => "SHADOW_DANCE_SONG_HBRGO_SAKI.level2Title",
            _ => "SHADOW_DANCE_SONG_HBRGO_SAKI.level3Title"
        };
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        int songLevel = SongLevel;
        int highSongAmount = HighSongAmount;
        Creature target = cardPlay.Target;
        await PowerCmd.Apply<ShadowDanceTemporaryStrengthPower>(
            choiceContext,
            target,
            highSongAmount * 2m,
            Owner.Creature,
            this);

        if (songLevel >= 2 && target.Player != null)
        {
            await PlayerCmd.GainEnergy(
                DynamicVars.Energy.BaseValue,
                target.Player);
            await CardPileCmd.Draw(choiceContext, 2m, target.Player);
        }

        if (songLevel >= 3)
        {
            // Extra turns belong to players; their phantoms act with them via PhantomPower.
            foreach (Creature ally in TeamCombatHelper.GetLivingPlayerCreatures(Owner.Creature))
            {
                await PowerCmd.Apply<ShadowDanceExtraTurnPower>(
                    choiceContext,
                    ally,
                    1m,
                    Owner.Creature,
                    this);
            }
        }

        await ScheduleHighSongReturn(PileType.Discard);
    }
}
