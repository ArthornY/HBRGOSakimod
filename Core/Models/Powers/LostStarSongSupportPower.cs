using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

/// <summary>Battle-long song upgrades enabled by playing Lost Star Song.</summary>
public sealed class LostStarSongSupportPower : PowerModel, ICustomPower
{
    // Match PowerModel.SetAmount's engine limit so a displayed initial amount
    // is always attainable when High Song is first applied or reset.
    private const int MaximumInitialHighSongAmount = 999999999;

    // Retained only for old combat saves. Their upgrade IDs now enable the
    // effects below; no code uses their former return/draw behavior.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ReturnHighSongToDrawTop { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool DrawAfterHighSong { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool FreeThirtyACardAfterHighSong { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool GainRockPotion { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IncreaseInitialHighSongAfterSong { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BoostRollWhileHighSong { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int InitialHighSongBonus { get; private set; }

    internal bool HasInitialHighSongGrowth => IncreaseInitialHighSongAfterSong || ReturnHighSongToDrawTop;
    internal bool HasHighSongRollBonus => BoostRollWhileHighSong || FreeThirtyACardAfterHighSong;

    internal static int GetInitialHighSongAmount(Creature creature) =>
        (int)Math.Min(MaximumInitialHighSongAmount, 1L + Math.Max(0,
            creature?.GetPower<LostStarSongSupportPower>()?.InitialHighSongBonus ?? 0));

    internal static int GetRollBonus(Creature creature) =>
        creature?.GetPower<HighSongPower>()?.Amount > 0 &&
        creature.GetPower<LostStarSongSupportPower>() is { Amount: > 0, HasHighSongRollBonus: true }
            ? 15 : 0;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override Color AmountLabelColor => new("F2A5D0");
    public string CustomPackedIconPath => "res://images/atlases/power_atlas.sprites/high_song_power.tres";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            if (!IsMutable) return base.Description;
            LocString text = new("powers", "LOST_STAR_SONG_SUPPORT_POWER.activeDescription");
            List<string> effects = new();
            if (HasInitialHighSongGrowth) effects.Add(EffectText(3));
            if (DrawAfterHighSong) effects.Add(EffectText(4));
            if (HasHighSongRollBonus) effects.Add(EffectText(5));
            if (GainRockPotion) effects.Add(EffectText(6));
            text.Add("Effects", string.Join("\n", effects));
            text.Add("InitialHighSongAmount", GetInitialHighSongAmount(Owner));
            return text;
        }
    }

    private static string EffectText(int id) =>
        new LocString("cards", "LOST_STAR_SONG_HBRGO_SAKI.upgrade_effect." + id).GetFormattedText();

    internal static async Task Enable(PlayerChoiceContext context, Player player, CardModel source,
        bool increaseInitialHighSong, bool drawAfterHighSong, bool boostHighSongRoll, bool gainPotion)
    {
        if (!(increaseInitialHighSong || drawAfterHighSong || boostHighSongRoll || gainPotion)) return;
        LostStarSongSupportPower power = player.Creature.GetPower<LostStarSongSupportPower>() ??
            await PowerCmd.Apply<LostStarSongSupportPower>(context, player.Creature, 1m, player.Creature, source);
        if (power == null) return;
        power.AssertMutable();
        // Different upgraded copies contribute their effects once for this battle.
        power.IncreaseInitialHighSongAfterSong |= increaseInitialHighSong;
        power.DrawAfterHighSong |= drawAfterHighSong;
        power.BoostRollWhileHighSong |= boostHighSongRoll;
        power.GainRockPotion |= gainPotion;
        player.Creature.InvokePowerModified(power, 0, true);
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player || !CanResolve()) return;
        if (cardPlay.Card is HighSongChoiceCard && HasInitialHighSongGrowth)
        {
            AssertMutable();
            InitialHighSongBonus = (int)Math.Min(MaximumInitialHighSongAmount - 1L,
                Math.Max(0L, InitialHighSongBonus) + 1L);
            Owner.InvokePowerModified(this, 0, true);
            Flash();
        }

        // High Song removes its token during OnPlay. Its completed play still
        // dispatches this hook, so do not require that token to remain in a pile.
        if (cardPlay.Card is not HighSongHbrgoSaki || !DrawAfterHighSong) return;

        Flash();
        await CardPileCmd.Draw(choiceContext, 2m, Owner.Player);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || !GainRockPotion || !CanResolve()) return;
        Flash();
        // Native procurement leaves a full potion belt unchanged.
        await PotionCmd.TryToProcure<PotionShapedRock>(player);
    }

    private bool CanResolve() => Amount > 0 && Owner.Player?.PlayerCombatState != null &&
        Owner.IsAlive && Owner.CombatState != null && !CombatManager.Instance.IsOverOrEnding;
}
