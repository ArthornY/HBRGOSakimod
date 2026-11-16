using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The garden's white space over shallow water, with glints on the water and falling flowers.
// The horizon follows the first row of cards; Sakura swaps the nemophila for cherry petals.
public partial class NMemoryGardenBackdrop : Control
{
    private const string ShaderPath = "res://shaders/hbr_memory_garden_backdrop.gdshader";
    private readonly ColorRect _space = new() { Name = "Space", MouseFilter = MouseFilterEnum.Ignore };
    private readonly NMemoryGardenParticles _particles = new() { Name = "Particles" };
    private float _horizon = 551;

    internal float Horizon
    {
        get => _horizon;
        set
        {
            _horizon = value;
            _particles.Horizon = value;
            (_space.Material as ShaderMaterial)?.SetShaderParameter("horizon", value);
        }
    }

    internal bool Sakura
    {
        get => _particles.Sakura;
        set => _particles.Sakura = value;
    }

    public NMemoryGardenBackdrop()
    {
        Name = "MemoryGardenBackdrop";
        MouseFilter = MouseFilterEnum.Stop;
        _space.Material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(ShaderPath) };
        AddChild(_space);
        AddChild(_particles);
    }

    public override void _Ready()
    {
        _space.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _particles.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Sync();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) Sync();
    }

    private void Sync()
    {
        (_space.Material as ShaderMaterial)?.SetShaderParameter("size", Size);
        Horizon = _horizon;
    }
}
