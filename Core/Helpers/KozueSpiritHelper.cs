using System;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
namespace HBRGOSakiMod.Core.Helpers;
internal static class KozueSpiritHelper
{
    internal static Creature GetLivingSpirit(Player player) => player?.PlayerCombatState?.Pets?.FirstOrDefault(pet => pet.IsAlive && pet.HasPower<KozueSpiritPower>());
    internal static decimal GetDamageAmplificationPercent(Creature dealer) =>
        dealer?.Player is { } player && GetLivingSpirit(player)?.GetPower<KozueSpiritPower>()?.Inspiration == true ? 15m : 0m;
    internal static MonsterModel[] Candidates(Player player)
    {
        RoomType room = player.Creature.CombatState.Encounter?.RoomType ?? player.RunState.CurrentRoom?.RoomType ?? RoomType.Monster;
        return player.RunState.Act.AllEncounters.Where(encounter => encounter.RoomType == room)
            .SelectMany(encounter => encounter.AllPossibleMonsters).Where(monster => monster != null && monster.ShouldShowInCompendium &&
                !PhantomRosterCandidateHelper.IsMinionOnlyModel(monster) && monster is not (BattleFriendV1 or BattleFriendV2 or BattleFriendV3))
            .GroupBy(monster => monster.Id).Select(group => group.First()).ToArray();
    }
    internal static decimal CalculateSpawnHealth(Player owner, MonsterModel canonical, decimal fraction)
    {
        // A detached enemy-side probe uses the native HP roll, multiplayer and
        // shared HBR enemy resources, without adding a ghost enemy/turn listener.
        MonsterModel probeModel = (MonsterModel)canonical.ToMutable();
        probeModel.RunRng = owner.RunState.Rng;
        Creature probe = new(probeModel, CombatSide.Enemy, null) { CombatState = owner.Creature.CombatState };
        probe.SetUniqueMonsterHpValue(owner.Creature.CombatState.Enemies, owner.RunState.Rng.Niche);
        probe.ScaleMonsterHpForMultiplayer(owner.Creature.CombatState.Encounter, owner.Creature.CombatState.Players.Count, owner.RunState.CurrentActIndex);
        EnemyHealthScalingHelper.ApplyInitialHealth(probe);
        return Math.Max(1m, decimal.Floor(LargeCreatureHealthHelper.GetMaxHp(probe) * fraction));
    }
    internal static async Task Summon(PlayerChoiceContext context, InnocentRequiemHbrgoSaki card)
    {
        Player owner = card.Owner;
        if (owner.Creature.IsDead || owner.Creature.CombatState is not { } combat) return;
        foreach (Creature old in owner.PlayerCombatState.Pets.Where(pet => pet.HasPower<KozueSpiritPower>()).ToArray())
        {
            await RemoveAuras(old);
            if (old.IsAlive) await CreatureCmd.Kill(old, force: true);
        }
        MonsterModel[] candidates = Candidates(owner);
        if (candidates.Length == 0) return;
        MonsterModel selected = candidates[owner.RunState.Rng.Niche.NextInt(candidates.Length)];
        decimal health = CalculateSpawnHealth(owner, selected, card.HasUpgradeEffect(InnocentRequiemHbrgoSaki.SoulEvocare) ? .6m : .3m);
        Creature spirit;
        using (PhantomAscensionHelper.BeginPhantomEvaluation())
        {
            spirit = combat.CreateCreature((MonsterModel)selected.ToMutable(), owner.Creature.Side, null);
            spirit.SetMaxHpInternal(health); spirit.SetCurrentHpInternal(health);
            await PlayerCmd.AddPet(spirit, owner);
            // Structural pet state and its entry resource are not card-value
            // grants. Match MomentOfReign: dice may scale authored effects,
            // never the pet marker/level or its fixed entry DP ratio.
            var marker = (KozueSpiritPower)ModelDb.Power<KozueSpiritPower>().ToMutable(); marker.Configure(card);
            await PowerCmd.Apply(context, marker, spirit, 1m, owner.Creature, null);
            var phantom = (PhantomPower)ModelDb.Power<PhantomPower>().ToMutable(); phantom.ConfigureAsSummonedMinion(1);
            await PowerCmd.Apply(context, phantom, spirit, 1m, owner.Creature, null);
            // MinionPower excludes this independent pet from every main-roster
            // survivor/experience path. It is not entered into MomentOfReign.
            await PowerCmd.Apply<MinionPower>(context, spirit, 1m, owner.Creature, null);
            await PhantomHelper.RunPhantomEntryEffects(spirit);
            spirit.SetMaxHpInternal(health); spirit.SetCurrentHpInternal(health);
            foreach (HighDimensionalShellPower shell in spirit.Powers.OfType<HighDimensionalShellPower>().ToArray()) await PowerCmd.Remove(shell);
            await PowerCmd.Apply<KozueSpiritShellPower>(context, spirit, decimal.Floor(health * .2m), owner.Creature, null);
            if (marker.Smile) await ApplyAuras(context, spirit, card);
            MomentOfReign.ArrangePhantomVisuals(spirit);
            spirit.PrepareForNextTurn(combat.GetOpponentsOf(spirit).Where(PhantomHelper.IsValidPhantomTarget));
        }
        NCombatRoom.Instance?.SetCreatureIsInteractableCompat(spirit, on: true);
        if (card.HasUpgradeEffect(InnocentRequiemHbrgoSaki.FreezingCurse))
        {
            using var batch = HbrDebuffApplicationScope.BeginBatch(owner.Creature);
            foreach (Creature enemy in combat.GetOpponentsOf(owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<FragilePower>(context, enemy, 46.875m, owner.Creature, card, profile: new HbrEffectProfile(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2));
        }
    }
    private static async Task ApplyAuras(PlayerChoiceContext context, Creature spirit, CardModel source)
    {
        var profile = new HbrEffectProfile(HbrEffectLimit.None, HbrEffectExit.Eternal, 1);
        var rate = (HbrCriticalRateUpPower)ModelDb.Power<HbrCriticalRateUpPower>().ToMutable();
        var damage = (HbrCriticalDamageUpPower)ModelDb.Power<HbrCriticalDamageUpPower>().ToMutable();
        Creature owner = spirit.PetOwner.Creature;
        await HbrEffectApplication.Apply(context, rate, owner, 50m, owner, source, profile: profile);
        await HbrEffectApplication.Apply(context, damage, owner, 50m, owner, source, profile: profile);
        // Potency uses the player's stats; provenance identifies exactly these
        // two instances so disappearance cannot remove an unrelated grant.
        if (rate.Owner != null) rate.Applier = spirit;
        if (damage.Owner != null) damage.Applier = spirit;
    }
    internal static async Task RemoveAuras(Creature spirit)
    {
        Creature owner = spirit?.PetOwner?.Creature;
        if (owner == null) return;
        foreach (HbrEffectPower power in owner.Powers.OfType<HbrEffectPower>().Where(power => power.Applier == spirit &&
            power is HbrCriticalRateUpPower or HbrCriticalDamageUpPower).ToArray()) await PowerCmd.Remove(power);
    }
}
