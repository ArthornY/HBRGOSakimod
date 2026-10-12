using System;
using System.Runtime.CompilerServices;
using Godot;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Helpers;

// Keep the native default/tween scale separate from the HP multiplier. In
// particular, an HP change during a shrink tween must not reuse its old target.
internal sealed class PhantomHealthScaleState
{
    internal PhantomHealthScaleState(float nativeDefaultScale) => NativeDefaultScale = nativeDefaultScale;

    internal float NativeDefaultScale { get; private set; }
    internal float HealthFactor { get; private set; } = 1f;
    internal float TweenHealthFactor { get; private set; } = 1f;
    internal float DefaultScale => NativeDefaultScale * HealthFactor;
    internal float TweenCorrection => HealthFactor / TweenHealthFactor;

    internal static float CalculateFactor(decimal maxHp, decimal baseMaxHp)
    {
        decimal baseline = Math.Max(1, baseMaxHp);
        // Each complete 5 HP gained over the unupgraded summon adds one percentage point.
        decimal steps = decimal.Floor(Math.Max(0m, maxHp - baseline) / 5m);
        return (float)(0.8m + steps * 0.01m);
    }

    internal float UpdateHealth(decimal maxHp, decimal baseMaxHp)
    {
        float next = CalculateFactor(maxHp, baseMaxHp);
        float ratio = next / HealthFactor;
        HealthFactor = next;
        return ratio;
    }

    internal float SetNativeDefault(float scale)
    {
        NativeDefaultScale = scale;
        return DefaultScale;
    }

    internal void BeginTween() => TweenHealthFactor = HealthFactor;
}

internal static class PhantomHealthScaleHelper
{
    private sealed class Entry
    {
        internal Node Bounds;
        internal PhantomHealthScaleState Scale;
    }

    private static readonly ConditionalWeakTable<NCreature, Entry> Entries = new();
    private static readonly Action<NCreature, Node> UpdateBounds =
        PrivateAccess.MethodDelegate<NCreature, Action<NCreature, Node>>("UpdateBounds", typeof(Node));

    internal static void RememberBounds(NCreature creature, Node bounds)
    {
        // _Ready runs before PhantomPower is applied. Remember the animation's
        // current bounds then as well, without changing any ordinary pet.
        if (creature.Entity?.PetOwner != null)
            Entries.GetOrCreateValue(creature).Bounds = bounds;
    }

    private static Entry GetEntry(NCreature creature)
    {
        if (creature == null || !GodotObject.IsInstanceValid(creature) ||
            !creature.IsInsideTree() ||
            !NPhantomCombatLayout.UsesHopperLayout(creature.Entity) ||
            creature.Visuals == null || !GodotObject.IsInstanceValid(creature.Visuals)) return null;
        Entry entry = Entries.GetOrCreateValue(creature);
        entry.Scale ??= new PhantomHealthScaleState(creature.Visuals.DefaultScale);
        return entry;
    }

    internal static void Refresh(NCreature creature)
    {
        Entry entry = GetEntry(creature);
        if (entry == null) return;
        PhantomPower power = creature.Entity.GetPower<PhantomPower>();
        float ratio = entry.Scale.UpdateHealth(LargeCreatureHealthHelper.GetMaxHp(creature.Entity), power.GetSizeBaseMaxHp());
        if (ratio == 1f) return;
        creature.Visuals.DefaultScale = entry.Scale.DefaultScale;
        // Apply only the change in factor; later calls with the same HP do nothing.
        // Body.Scale retains its authored scale and the summon-facing mirror.
        creature.Visuals.Scale *= ratio;
        RefreshBounds(creature, entry);
    }

    internal static void ScaleNativeDefault(NCreature creature, ref float scale)
    {
        Entry entry = GetEntry(creature);
        if (entry != null) scale = entry.Scale.SetNativeDefault(scale);
    }

    internal static void BeginTween(NCreature creature)
    {
        GetEntry(creature)?.Scale.BeginTween();
    }

    internal static void AdjustTween(NCreature creature, ref Vector2 scale)
    {
        Entry entry = GetEntry(creature);
        if (entry != null) scale *= entry.Scale.TweenCorrection;
    }

    internal static void RefreshBounds(NCreature creature)
    {
        Entry entry = GetEntry(creature);
        if (entry != null) RefreshBounds(creature, entry);
    }

    private static void RefreshBounds(NCreature creature, Entry entry)
    {
        if (creature.Hitbox == null || creature.IntentContainer == null) return;
        Node bounds = entry.Bounds;
        if (bounds == null || !GodotObject.IsInstanceValid(bounds) || !bounds.IsInsideTree())
            bounds = creature.Visuals;
        UpdateBounds(creature, bounds);
    }
}
