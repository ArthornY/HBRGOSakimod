using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class TengenBrilliantMoveHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneDMemberCard
{
    internal const int Flash = 0, Cooldown = 1, StoneTower = 2, SnowWaltz = 3,
        MoreDessert = 4, Peerless = 5, FiftyWins = 6;
    private decimal _growth, _appliedGrowth;
    protected override int FlashEffectId => Flash;
    public ThirtyOneDMember ThirtyOneDMember => ThirtyOneDMember.NikaidoMisato;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string WinningStreakData
    {
        get => _growth == 0m ? string.Empty : _growth.ToString("0", CultureInfo.InvariantCulture);
        private set => _growth = decimal.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out decimal parsed) ? Math.Max(0m, decimal.Floor(parsed)) : 0m;
    }
    internal decimal GrowthPercent => _growth;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { HbrSkillVars.Damage(40m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrDefenseDownPower>(),
      HoverTipFactory.FromPower<HbrComboLargePower>(), HoverTipFactory.FromPower<MinionPower>() }
        .Append(HoverTipCompatibility.CreateRelicReference<HBRGOSakiMod.Core.Models.Relics.Overclock>());
    public TengenBrilliantMoveHbrgoSaki() : base(1, CardType.Attack, TargetType.AllEnemies) { }

    // Add only our growth delta, preserving unrelated base-damage changes.
    internal void SetGrowth(decimal value)
    { _growth = Math.Max(0m, value); SyncDamage(); }
    internal void SyncDamage()
    {
        if (!IsMutable) return;
        decimal delta = _growth - _appliedGrowth;
        if (delta != 0m) DynamicVars.Damage.BaseValue += HbrSkillPower.DamageUnits(delta);
        _appliedGrowth = _growth;
    }
    public override Task BeforeCombatStart()
    { TengenBrilliantMoveCombatHelper.Synchronize(this); return Task.CompletedTask; }
    protected override void AfterCloned()
    { base.AfterCloned(); TengenBrilliantMoveCombatHelper.Track(this); }
    protected override void AfterDeserialized()
    { base.AfterDeserialized(); SyncDamage(); }
    protected override void AfterDowngraded()
    { base.AfterDowngraded(); _appliedGrowth = 0m; SyncDamage(); }
    protected override void AddExtraArgsToDescription(LocString text)
    {
        TengenBrilliantMoveCombatHelper.Synchronize(this);
        base.AddExtraArgsToDescription(text);
        text.Add("MisatoAttackUp", HbrCardFaceDescription.Percent(this, "AttackUp", HasUpgradeEffect(Cooldown) ? 75m : 35m, true));
        text.Add("MisatoGrowth", _growth.ToString("0", CultureInfo.InvariantCulture));
        text.Add("MisatoPerHit", (40m + _growth).ToString("0", CultureInfo.InvariantCulture));
        text.Add("MisatoGrowthText", string.Empty);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        TengenBrilliantMoveCombatHelper.Synchronize(this);
        if (HasUpgradeEffect(MoreDessert) && TengenBrilliantMoveCombatHelper.IsFirstOrdinaryTurn(Owner))
            await PlayerCmd.GainEnergy(2m, Owner);
        if (HasUpgradeEffect(SnowWaltz) && TengenBrilliantMoveCombatHelper.IsOverdrive(this))
            await HbrComboPower.Grant<HbrComboLargePower>(context, Owner.Creature, Owner.Creature, this);
        if (HasUpgradeEffect(Peerless))
        {
            using var batch = HbrDebuffApplicationScope.BeginBatch(Owner.Creature);
            foreach (var enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, enemy, 20m,
                    Owner.Creature, this, new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2));
        }
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, play)
            .TargetingAllOpponents(CombatState).WithHitCount(3).WithHitFx("vfx/vfx_attack_blunt").Execute(context);
        await HbrEffectApplication.Apply<HbrAttackUpPower>(context, Owner.Creature,
            HasUpgradeEffect(Cooldown) ? 75m : 35m, Owner.Creature, this,
            remaining: 1, profile: new(HbrEffectLimit.Default, HbrEffectExit.Count, 1));
    }
}
