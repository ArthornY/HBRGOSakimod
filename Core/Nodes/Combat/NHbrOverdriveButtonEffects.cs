using System;
using Godot;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// These effects belong to the OD button prefab itself, not the battle's
// full-screen OverDrive effect. One canvas and fixed slots avoid per-frame
// node, texture, material, or collection allocation.
internal partial class NHbrOverdriveButtonEffects : Control
{
    private enum Kind { StandbyRing, StandbySpark, PushRing, PushSpark, ActivateRing, ActivateFlash, ActivateSpark }
    private struct Particle
    {
        internal bool Alive;
        internal Kind Kind;
        internal float Age;
        internal float Life;
        internal float Angle;
        internal float Radius;
        internal float Variation;
    }

    private const string AssetPath = "res://images/ui/overclock/original/";
    private readonly Particle[] _particles = new Particle[128];
    private Texture2D _ringTexture;
    private Texture2D _sparkTexture;
    private Texture2D _flashTexture;
    private uint _sequence;
    private bool _standby;
    private float _standbyTime;
    private float _pushTime = -1f;
    private float _activateTime = -1f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _ringTexture = ResourceLoader.Load<Texture2D>(AssetPath + "particleAuraCircle.png");
        _sparkTexture = ResourceLoader.Load<Texture2D>(AssetPath + "infoeffectParticle.png");
        _flashTexture = ResourceLoader.Load<Texture2D>(AssetPath + "flasheffect.png");
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    }

    internal void SetStandby(bool enabled)
    {
        if (_standby == enabled) return;
        _standby = enabled;
        _standbyTime = 0f;
        Clear(Kind.StandbyRing, Kind.StandbySpark);
        if (enabled) Spawn(Kind.StandbyRing, 0f);
    }

    internal void Push()
    {
        CancelPush();
        _pushTime = 0f;
        Spawn(Kind.PushRing, 0f);
    }

    internal void CancelPush()
    {
        _pushTime = -1f;
        Clear(Kind.PushRing, Kind.PushSpark);
    }

    internal void Activate()
    {
        SetStandby(false);
        CancelPush();
        Clear(Kind.ActivateRing, Kind.ActivateFlash, Kind.ActivateSpark);
        _activateTime = 0f;
        Spawn(Kind.ActivateRing, 0f);
        Spawn(Kind.ActivateFlash, 0f);
    }

    internal void ResetEffects()
    {
        _standby = false;
        _standbyTime = 0f;
        _pushTime = _activateTime = -1f;
        Array.Clear(_particles);
        QueueRedraw();
    }

    internal void Advance(float delta)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            if (!_particles[i].Alive) continue;
            _particles[i].Age += delta;
            if (_particles[i].Age >= _particles[i].Life) _particles[i].Alive = false;
        }

        if (_standby)
        {
            float previous = _standbyTime;
            _standbyTime += delta;
            Emit(Kind.StandbySpark, previous, _standbyTime, float.PositiveInfinity, 30f);
            // Original one-second loop: two single-ring bursts, 0 and 0.35 s.
            int firstLoop = Math.Max(0, (int)Math.Floor(_standbyTime - 1f));
            for (int loop = firstLoop; loop <= (int)Math.Floor(_standbyTime); loop++)
            {
                EmitRingAt(loop, previous, _standbyTime);
                EmitRingAt(loop + 0.35f, previous, _standbyTime);
            }
        }
        if (_pushTime >= 0f)
        {
            float previous = _pushTime;
            _pushTime += delta;
            Emit(Kind.PushSpark, previous, _pushTime, 0.2f, 150f);
            if (_pushTime >= 0.45f) _pushTime = -1f;
        }
        if (_activateTime >= 0f)
        {
            float previous = _activateTime;
            _activateTime += delta;
            Emit(Kind.ActivateSpark, previous, _activateTime, 0.25f, 150f);
            if (_activateTime >= 1.25f) _activateTime = -1f;
        }
        QueueRedraw();
    }

    private void EmitRingAt(float birth, float previous, float now)
    {
        if (birth > previous && birth <= now && now - birth < 0.6f)
            Spawn(Kind.StandbyRing, now - birth);
    }

    private void Emit(Kind kind, float previous, float now, float duration, float rate)
    {
        int first = Math.Max((int)Math.Floor(previous * rate) + 1,
            (int)Math.Ceiling(Math.Max(0f, now - Life(kind)) * rate));
        int last = (int)Math.Floor(Math.Min(now, duration) * rate);
        for (int sample = first; sample <= last; sample++)
            Spawn(kind, now - sample / rate);
    }

    private void Spawn(Kind kind, float age)
    {
        if (age >= Life(kind)) return;
        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i].Alive) continue;
            uint seed = ++_sequence;
            float variation = Random01(seed * 3u);
            float radius = kind switch
            {
                Kind.StandbySpark => MathF.Sqrt(0.375f * 0.375f + variation * (0.5f * 0.5f - 0.375f * 0.375f)),
                Kind.PushSpark => MathF.Sqrt(1.125f * 1.125f + variation * (1.5f * 1.5f - 1.125f * 1.125f)),
                Kind.ActivateSpark => MathF.Sqrt(variation),
                _ => 0f
            };
            _particles[i] = new Particle
            {
                Alive = true, Kind = kind, Age = age, Life = Life(kind), Radius = radius,
                Angle = Random01(seed * 3u + 1u) * Mathf.Tau,
                Variation = Random01(seed * 3u + 2u)
            };
            return;
        }
    }

    public override void _Draw()
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            Particle p = _particles[i];
            if (!p.Alive) continue;
            float t = p.Age / p.Life;
            float radius = p.Radius, width, length;
            Texture2D texture;
            switch (p.Kind)
            {
                case Kind.StandbySpark:
                    radius += HbrOverdriveParticleCurves.StandbyDistance(t);
                    width = 10f * t;
                    length = width * 3f;
                    texture = _sparkTexture;
                    break;
                case Kind.PushSpark:
                    radius += HbrOverdriveParticleCurves.PushDistance(t);
                    width = 10f;
                    length = width * 2f + 7f * HbrOverdriveParticleCurves.Contract(t) * 0.25f * 100f;
                    texture = _sparkTexture;
                    break;
                case Kind.ActivateSpark:
                    radius += 15f * p.Variation * HbrOverdriveParticleCurves.ActivateDistance(t);
                    width = 10f;
                    length = width * 2f + 15f * p.Variation * HbrOverdriveParticleCurves.ActivateVelocity(t) * 100f;
                    texture = _sparkTexture;
                    break;
                default:
                    width = p.Kind switch
                    {
                        Kind.StandbyRing => 140f * HbrOverdriveParticleCurves.Contract(t),
                        Kind.PushRing => 350f * HbrOverdriveParticleCurves.Contract(t),
                        Kind.ActivateFlash => 400f * HbrOverdriveParticleCurves.Contract(t),
                        _ => 500f * HbrOverdriveParticleCurves.ActivateRing(t)
                    };
                    length = width;
                    texture = p.Kind == Kind.ActivateFlash ? _flashTexture : _ringTexture;
                    break;
            }
            if (texture == null || width <= 0f) continue;
            HbrOverdriveParticleCurves.Rgba c = HbrOverdriveParticleCurves.Color(t);
            Vector2 direction = Vector2.FromAngle(p.Angle);
            DrawSetTransform(direction * (radius * 100f), p.Kind is Kind.StandbySpark or Kind.PushSpark or Kind.ActivateSpark ? p.Angle : 0f);
            DrawTextureRect(texture, new Rect2(-length / 2f, -width / 2f, length, width), false,
                new Color(c.R, c.G, c.B, c.A));
        }
        DrawSetTransform(Vector2.Zero);
    }

    private void Clear(Kind first, Kind second, Kind third = (Kind)(-1))
    {
        for (int i = 0; i < _particles.Length; i++)
            if (_particles[i].Kind == first || _particles[i].Kind == second || _particles[i].Kind == third)
                _particles[i].Alive = false;
        QueueRedraw();
    }

    private static float Life(Kind kind) => kind switch
    {
        Kind.StandbyRing => 0.6f,
        Kind.StandbySpark => 1.5f,
        Kind.PushRing => 0.35f,
        Kind.PushSpark => 0.25f,
        Kind.ActivateFlash => 0.5f,
        _ => 1f
    };

    // UI-only stochastic samples, independent from combat RNG or saved state.
    private static float Random01(uint value)
    {
        value ^= value >> 16;
        value *= 0x7feb352du;
        value ^= value >> 15;
        value *= 0x846ca68bu;
        value ^= value >> 16;
        return (value & 0x00ffffffu) / 16777216f;
    }
}
