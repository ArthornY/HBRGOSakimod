using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Cards;

public partial class NPhantomPortraitRenderer : Node
{
    // Stable returns MegaTrackEntry; beta returns void. Resolve by parameters so
    // the portrait renderer does not bind to either build's return type.
    private static readonly MethodInfo SetAnimationMethod = typeof(MegaAnimationState).GetMethod(
        nameof(MegaAnimationState.SetAnimation), new[] { typeof(string), typeof(bool), typeof(int) });

    private readonly Queue<(MonsterModel Monster, ImageTexture Texture, PhantomPortraitStyle Style, Action<bool, bool> Completed)> _queue = new();
    private bool _rendering;
    private string _activeMonsterId;
    private string _renderStage;

    internal void Enqueue(MonsterModel monster, ImageTexture texture, PhantomPortraitStyle style, Action<bool, bool> completed) =>
        _queue.Enqueue((monster, texture, style, completed));

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        if (!_rendering && _queue.Count > 0)
        {
            _rendering = true;
            var request = _queue.Dequeue();
            _ = RenderRequest(request.Monster, request.Texture, request.Style, request.Completed);
        }
    }

    private async Task RenderRequest(MonsterModel monster, ImageTexture texture, PhantomPortraitStyle style,
        Action<bool, bool> completed)
    {
        bool succeeded = false;
        bool retryable = true;
        try
        {
            _activeMonsterId = style == PhantomPortraitStyle.Cutout ? $"{monster.Id} (cutout)" : monster.Id.ToString();
            TraceStage("begin");
            // Keep queue ownership outside the rendering state machine. An entry
            // failure must also release the queue, not strand every later card.
            await Render(monster, texture, style);
            succeeded = true;
        }
        catch (Exception exception)
        {
            retryable = exception is not PhantomPortraitUnavailableException;
            GD.PushWarning($"Phantom portrait {_activeMonsterId} at {_renderStage}: {exception}");
        }
        finally
        {
            _rendering = false;
            completed(succeeded, retryable);
            TraceStage(succeeded ? "complete" : "failed");
        }
    }

    // Native renderer failures can terminate the process without a managed stack.
    // Record each boundary once per portrait, never once per frame.
    private void TraceStage(string stage)
    {
        _renderStage = stage;
        GD.Print($"[HBRGO PhantomPortrait] {_activeMonsterId}: {stage}");
    }

    internal static string GetStaticPortraitPath(MonsterModel monster) => monster switch
    {
        FlatHand => "res://images/monsters/hbrgo_act4/flat_hand_phase12.png",
        SkullFeather => "res://images/map/boss_nodes/skull_feather.png",
        DeathSlug => "res://images/map/boss_nodes/death_slug.png",
        // These claws share a native Spine scene whose portrait draw can terminate
        // the process. Use portraits exported from its original art instead.
        Rocket => "res://images/packed/card_portraits/phantoms/rocket.png",
        Crusher => "res://images/packed/card_portraits/phantoms/crusher.png",
        _ => null
    };

    private async Task Render(MonsterModel monster, ImageTexture texture, PhantomPortraitStyle style)
    {
        SubViewport viewport = null;
        try
        {
            string isolatedPath = GetStaticPortraitPath(monster);
            if (isolatedPath != null)
            {
                TraceStage("load-static-image");
                using Image isolated = ResourceLoader.Load<Texture2D>(isolatedPath).GetImage();
                await UpdateTexture(texture, isolated, style);
                return;
            }

            TraceStage("create-viewport");
            viewport = new SubViewport
            {
                Size = new Vector2I(1024, 768), TransparentBg = true,
                Disable3D = true, GuiDisableInput = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always
            };
            AddChild(viewport);

            // Render visuals only: never create a combat actor, consume RNG, or run monster moves.
            TraceStage("create-visuals");
            NCreatureVisuals visuals = monster.CreateVisuals();
            // Frame a separate parent so imported scenes retain their root scale,
            // rotation and offsets, including non-Spine mod scenes.
            Node2D framingRoot = new();
            viewport.AddChild(framingRoot);
            framingRoot.AddChild(visuals);
            TraceStage("configure-skin");
            SetPortraitSkin(visuals, monster);
            visuals.UpdatePhobiaMode(monster);
            HideParticles(visuals);
            if (visuals.HasSpineAnimation && !visuals.IsUsingPhobiaModeBody)
            {
                TraceStage("apply-spine-pose");
                // Scene preview animations are editor-only. Without an applied idle pose,
                // setup attachments can be detached or leave enormous unused bounds.
                SetPortraitPose(visuals.SpineBody);
                if (monster is CeremonialBeast)
                    PhantomPortraitSpineSlots.HideSlots(visuals.SpineBody,
                        "plow start target", "plow end target");
                HideBones(visuals.SpineBody, "shadow");
            }
            else
            {
                TraceStage("apply-sprite-pose");
                SetSpritePortraitPose(visuals.GetCurrentBody());
            }
            TraceStage("wait-for-pose-draw");
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            TraceStage("measure-bounds");
            Node2D body = visuals.GetCurrentBody();
            Rect2 bounds = visuals.HasSpineAnimation && !visuals.IsUsingPhobiaModeBody
                ? RelativeTransform(framingRoot, body) * visuals.SpineBody.GetSkeleton().GetBounds()
                : GetSpriteBounds(framingRoot, body) ?? default;
            if (!HasUsableBounds(bounds))
                bounds = RelativeTransform(framingRoot, visuals.Bounds)
                    * new Rect2(Vector2.Zero, visuals.Bounds.Size);
            TraceStage($"portrait-body {body.GetClass()} bounds={bounds}");
            await CapturePortrait(viewport, texture, framingRoot, bounds, style);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(viewport)) viewport.QueueFree();
        }
    }

    private static Transform2D RelativeTransform(Node2D root, CanvasItem item) =>
        root.GlobalTransform.AffineInverse() * item.GetGlobalTransform();

    private static bool HasUsableBounds(Rect2 bounds) =>
        float.IsFinite(bounds.Position.X) && float.IsFinite(bounds.Position.Y)
        && float.IsFinite(bounds.Size.X) && float.IsFinite(bounds.Size.Y)
        && bounds.Size.X > 0 && bounds.Size.Y > 0;

    private static void SetSpritePortraitPose(Node node)
    {
        if (node is AnimatedSprite2D sprite && sprite.SpriteFrames is { } frames)
        {
            // BaseLib also supports frame animations. An empty default animation
            // does not show a body until combat normally selects the idle state.
            // Select a still directly without firing a combat animation callback.
            foreach (string name in new[] { "idle_loop", "idle", "Idle", sprite.Animation.ToString(), "default" })
            {
                if (!frames.HasAnimation(name)) continue;
                int count = frames.GetFrameCount(name);
                int frame = 0;
                while (frame < count && frames.GetFrameTexture(name, frame) == null) frame++;
                if (frame == count) continue;
                bool wasBlockingSignals = sprite.IsBlockingSignals();
                sprite.SetBlockSignals(true);
                try
                {
                    sprite.Pause();
                    sprite.Animation = name;
                    sprite.SetFrameAndProgress(frame, 0);
                }
                finally
                {
                    sprite.SetBlockSignals(wasBlockingSignals);
                }
                break;
            }
        }
        foreach (Node child in node.GetChildren()) SetSpritePortraitPose(child);
    }

    private static Rect2? GetSpriteBounds(Node2D root, Node node)
    {
        if (node is CanvasItem item && !item.Visible) return null;
        Rect2? bounds = null;
        if (node is Sprite2D sprite && sprite.Texture != null)
            bounds = RelativeTransform(root, sprite) * sprite.GetRect();
        else if (node is AnimatedSprite2D animated && animated.SpriteFrames is { } frames
            && frames.HasAnimation(animated.Animation)
            && animated.Frame >= 0 && animated.Frame < frames.GetFrameCount(animated.Animation)
            && frames.GetFrameTexture(animated.Animation, animated.Frame) is { } texture)
        {
            Vector2 size = texture.GetSize();
            Vector2 offset = animated.Offset - (animated.Centered ? size / 2 : Vector2.Zero);
            // Godot flips texture coordinates within this rectangle; FlipH/FlipV
            // do not reflect Offset or move the rectangle to the other side.
            bounds = RelativeTransform(root, animated) * new Rect2(offset, size);
        }
        if (bounds is { } own && !HasUsableBounds(own)) bounds = null;
        foreach (Node child in node.GetChildren())
        {
            if (GetSpriteBounds(root, child) is { } childBounds)
                bounds = bounds is { } current ? current.Merge(childBounds) : childBounds;
        }
        return bounds;
    }

    private static void SetPortraitSkin(NCreatureVisuals visuals, MonsterModel monster)
    {
        if (monster is Queen && visuals.SpineBody is { } queenSpine)
        {
            // Queen's skin setup starts track 1, whose native VFX callback reads
            // track 0. Portraits have no combat animator to initialize that track.
            SetPortraitPose(queenSpine);
        }

        if (monster is not PhantasmalGardener || visuals.SpineBody is not { } spine)
        {
            visuals.SetUpSkin(monster);
            return;
        }

        // Canonical portrait models have no Creature/encounter slot. The gardener's
        // combat SetupSkins reads that slot unconditionally; use its default short
        // variant without constructing an actor or mutating the canonical model.
        MegaSkeleton skeleton = spine.GetSkeleton();
        MegaSkeletonDataResource data = skeleton.GetData();
        MegaSkin skin = data.FindSkin("short");
        skeleton.SetSkin(skin);
        skeleton.SetSlotsToSetupPose();
        GC.KeepAlive(skin);
        GC.KeepAlive(data);
        GC.KeepAlive(skeleton);
        GC.KeepAlive(spine);
    }

    private async Task CapturePortrait(SubViewport viewport, ImageTexture texture, Node2D visuals, Rect2 bounds,
        PhantomPortraitStyle style)
    {
        if (!HasUsableBounds(bounds))
            throw new PhantomPortraitUnavailableException("Monster has no usable portrait bounds.");
        float scale = Math.Min(900f / bounds.Size.X, 660f / bounds.Size.Y);
        visuals.Scale = Vector2.One * scale;
        visuals.Position = new Vector2(512, 384) - bounds.GetCenter() * scale;
        TraceStage("capture-scaled-portrait");
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        visuals.ProcessMode = ProcessModeEnum.Disabled;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        TraceStage("read-viewport-image");
        using Image captured = viewport.GetTexture().GetImage();
        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        TraceStage("update-card-texture");
        await UpdateTexture(texture, captured, style);
    }

    private static void SetPortraitPose(MegaSprite spine)
    {
        MegaAnimationState state = spine.GetAnimationState();
        if (spine.HasAnimation("idle_loop"))
            SetAnimation(state, "idle_loop");
        else if (spine.HasAnimation("idle"))
            SetAnimation(state, "idle");
        FreezePose(spine);
    }

    private static void SetAnimation(MegaAnimationState state, string animation, int track = 0) =>
        SetAnimationMethod.Invoke(state, new object[] { animation, true, track });

    private static void FreezePose(MegaSprite spine)
    {
        MegaAnimationState state = spine.GetAnimationState();
        // Apply a deterministic still without constructing a combat animator or advancing RNG.
        for (int trackId = 0; trackId < 3; trackId++)
        {
            MegaTrackEntry track = state.GetCurrent(trackId);
            // Stable can wrap a null native object for an unused track; beta
            // returns null instead. Both mean there is no pose to freeze.
            if (track?.BoundObject == null) continue;
            track.SetMixDuration(0f);
            track.SetTrackTime(0.1f);
        }
        state.SetTimeScale(0f);
        state.Update(0f);
        state.Apply(spine.GetSkeleton());
    }

    private static void HideBones(MegaSprite spine, params string[] names)
    {
        var bones = new List<MegaBone>();
        foreach (string name in names)
        {
            if (spine.GetSkeleton().FindBone(name) is { } bone) bones.Add(bone);
        }
        // Animation updates can restore bone scales; hide them again before each world update.
        spine.ConnectBeforeWorldTransformsChange(Callable.From<GodotObject>(_ =>
        {
            foreach (MegaBone bone in bones) bone.Hide();
        }));
        foreach (MegaBone bone in bones) bone.Hide();
    }

    private static void HideParticles(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is GpuParticles2D gpu)
            {
                gpu.Emitting = false;
                gpu.Visible = false;
                gpu.ProcessMode = ProcessModeEnum.Disabled;
            }
            else if (child is CpuParticles2D cpu)
            {
                cpu.Emitting = false;
                cpu.Visible = false;
                cpu.ProcessMode = ProcessModeEnum.Disabled;
            }
            HideParticles(child);
        }
    }

    private async Task UpdateTexture(ImageTexture texture, Image source, PhantomPortraitStyle style)
    {
        if (source.IsCompressed()) source.Decompress();
        source.Convert(Image.Format.Rgba8);
        byte[] rgba = source.GetData();
        if (!HasVisiblePixels(rgba))
            throw new InvalidOperationException("Monster portrait rendered no visible pixels.");
        int width = source.GetWidth(), height = source.GetHeight();
        SceneTree tree = GetTree();
        // The bounds scan and bilinear sampling touch only managed pixels and
        // can otherwise consume a whole combat frame for each new monster.
        Task<byte[]> composition = Task.Run(() => style == PhantomPortraitStyle.Cutout
            ? PhantomPortraitCompositor.ComposeCutout(rgba, width, height)
            : PhantomPortraitCompositor.Compose(rgba, width, height));
        // Observe the worker from a game-thread signal instead of awaiting its
        // continuation: all Godot image and texture calls must stay on that thread.
        while (!composition.IsCompleted)
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            EnsureTextureTargetUsable(texture);
        }
        byte[] pixels = composition.GetAwaiter().GetResult();
        EnsureTextureTargetUsable(texture);
        using Image portrait = Image.CreateFromData(PhantomPortraitCompositor.Width, PhantomPortraitCompositor.Height,
            false, Image.Format.Rgba8, pixels);
        texture.Update(portrait);
    }

    private void EnsureTextureTargetUsable(ImageTexture texture)
    {
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() ||
            !GodotObject.IsInstanceValid(texture))
            throw new OperationCanceledException("The portrait renderer left the scene tree.");
    }

    private static bool HasVisiblePixels(byte[] rgba)
    {
        for (int index = 3; index < rgba.Length; index += 4)
            if (rgba[index] > 16) return true;
        return false;
    }
}

// A body with no usable visual or interaction bounds cannot be framed. Empty
// pixels alone remain retryable: custom scenes may finish loading after capture.
internal sealed class PhantomPortraitUnavailableException : InvalidOperationException
{
    internal PhantomPortraitUnavailableException(string message) : base(message) { }
}
