using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2RitsuLib.Scaffolding.MonsterMoves;

namespace HBRGOSakiMod.Core.Models.Monsters;

[CustomID("HOTARU_FINAL_CANNON")]
public sealed class HotaruFinalCannon : HotaruCannonModel
{
    public override HotaruCannonKind Kind => HotaruCannonKind.Endgame;
    protected override string SceneName => "endgame";
    public override LocString Title => new("monsters", Id.Entry + (Relic == null ? ".name"
        : Relic.EndgameSign > 0 ? ".redemptionName" : ".sacrificeName"));
    internal override decimal CurrentDamage => Relic?.EndgameBaseDamage ?? 0m;

    internal override LocString IntentDescription()
    {
        bool positive = Relic?.EndgameSign > 0;
        Creature cannon = IsMutable ? Creature : null;
        LocString shape = new("monsters", Id.Entry + (positive ? ".positiveShape" : ".negativeShape"));
        shape.Add("Count", Relic?.RetiredCannonCount ?? 0);
        shape.Add("Soul", Relic?.EndgameSoulPercent ?? (positive ? 80 : 50));
        shape.Add("BaseDamage", HotaruMyIndulgencePower.ModifyBaseDamage(cannon, CurrentDamage));
        shape.Add("Formula", positive ? "失去炮数!"
            : Relic?.WasCannonRetired(HotaruCannonKind.Happiness) == true ? "3^失去炮数−1" : "2^失去炮数");
        int startRepeat = HotaruCannonVigorHelper.GetPlannedHitCount(Relic, Kind, HotaruCannonPhase.Start);
        if (positive)
        {
            shape.Add("StartDamage", HotaruCannonStatsHelper.FormatDamagePreview(cannon, CurrentDamage,
                previewPhase: HotaruCannonPhase.Start));
            shape.Add("StartRepeat", startRepeat);
        }
        else
        {
            shape.Add("EndDamage", HotaruCannonStatsHelper.FormatDamagePreview(cannon, CurrentDamage,
                previewPhase: HotaruCannonPhase.End));
            shape.Add("EndRepeat", HotaruCannonVigorHelper.GetPlannedHitCount(Relic, Kind, HotaruCannonPhase.End));
            string extraStart = string.Empty;
            if (startRepeat > 0)
            {
                LocString start = new("monsters", Id.Entry + ".extraStart");
                start.Add("StartDamage", HotaruCannonStatsHelper.FormatDamagePreview(cannon, CurrentDamage,
                    previewPhase: HotaruCannonPhase.Start));
                start.Add("StartRepeat", startRepeat);
                extraStart = start.GetFormattedText();
            }
            shape.Add("ExtraStart", extraStart);
        }

        List<string> effects = new();
        if (Relic != null)
        {
            foreach (HotaruCannonKind kind in new[] { HotaruCannonKind.Happiness, HotaruCannonKind.Coffin,
                         HotaruCannonKind.Contest, HotaruCannonKind.Longing, HotaruCannonKind.Absolution })
            {
                if (!Relic.WasCannonRetired(kind)) continue;
                LocString effect = new("monsters", Id.Entry + (positive ? ".positive." : ".negative.") +
                    kind.ToString().ToLowerInvariant());
                effects.Add("• " + effect.GetFormattedText());
            }
        }

        LocString text = new("monsters", Id.Entry + ".intentDescription");
        text.Add("Shape", shape.GetFormattedText());
        text.Add("LostEffects", effects.Count == 0 ? string.Empty
            : "\n\n[gold]失去浮游炮后获得的效果[/gold]\n" + string.Join("\n", effects));
        int repeats = Relic?.StateFor(Kind)?.PendingActionRepeats ?? 0;
        text.Add("Charged", repeats > 0 ? "\n\n[gold]充能：下一次完整行动额外触发" + repeats +
            "次。实际触发时消耗；活力仅作用于首轮完整行动。[/gold]" : string.Empty);
        return text;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        List<AbstractIntent> intents = new();
        if (Relic?.EndgameStartHitCount > 0)
            intents.Add(new FinalAttackIntent(this, HotaruCannonPhase.Start));
        if (Relic?.EndgameEndHitCount > 0)
            intents.Add(new FinalAttackIntent(this, HotaruCannonPhase.End));
        MoveState idle = new("HOTARU_FINAL_CANNON_IDLE", _ => Task.CompletedTask, intents.ToArray());
        return ModMonsterMoveStateMachines.SingleMoveLoop(idle);
    }

    private sealed class FinalAttackIntent : SingleAttackIntent
    {
        private readonly HotaruFinalCannon _cannon;
        private readonly HotaruCannonPhase _phase;
        private int Repeats => HotaruCannonVigorHelper.GetPlannedHitCount(_cannon.Relic, _cannon.Kind, _phase);

        internal FinalAttackIntent(HotaruFinalCannon cannon, HotaruCannonPhase phase)
            : base(() => cannon.CurrentDamage)
        { _cannon = cannon; _phase = phase; }

        public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner) =>
            BigCombatNumbers.ToNativeSigned(DamageAmplificationLimits.MultiplySaturated(
                HotaruCannonStatsHelper.PreviewDamage(owner,
                    _cannon.CurrentDamage, previewPhase: _phase), Repeats));

        public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
        {
            int repeats = Repeats;
            LocString text = new("intents", repeats > 1 ? "FORMAT_DAMAGE_MULTI" : "FORMAT_DAMAGE_SINGLE");
            text.Add("Damage", HotaruCannonStatsHelper.FormatDamagePreview(owner,
                _cannon.CurrentDamage, previewPhase: _phase));
            if (repeats > 1) text.Add("Repeat", repeats);
            return text;
        }

        protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner) =>
            _cannon.IntentDescription();
    }
}
