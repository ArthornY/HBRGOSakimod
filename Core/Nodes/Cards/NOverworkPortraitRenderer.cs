using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;

namespace HBRGOSakiMod.Core.Nodes.Cards;

// One process-wide page renderer. Network work belongs to the session feed and
// never touches Godot. All viewport, font, image and texture work stays here on
// the Godot main thread; there are no async continuations tied to a card node.
public partial class NOverworkPortraitRenderer : Node
{
    private const int PageWidth = 1024;
    private const int PageHeight = 720;
    private const int MaximumDisplayedPosts = 4;
    private readonly List<Label> _postHeaders = new();
    private readonly List<Label> _postBodies = new();
    private ImageTexture _portraitTexture;
    private byte[] _originalPixels;
    private SubViewport _pageViewport;
    private Label _title;
    private Label _pageStatus;
    private Label _footer;
    private Font _font;
    private Callable _afterDraw;
    private Task<byte[]> _compositionTask;
    private long _renderedRevision = -1;
    private double _elapsed;
    private bool _capturePending;
    private bool _drawSignalConnected;

    internal void Initialize(ImageTexture texture, byte[] originalPixels)
    {
        _portraitTexture = texture;
        _originalPixels = originalPixels;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            BuildPage();
            _afterDraw = Callable.From(CapturePage);
            RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, _afterDraw);
            _drawSignalConnected = true;
            RefreshPage();
        }
        catch (Exception exception)
        {
            StopRendering(exception);
        }
    }

    public override void _Process(double delta)
    {
        if (_compositionTask != null)
        {
            if (_compositionTask.IsCompleted) ApplyComposition();
            return;
        }
        _elapsed += delta;
        if (_elapsed < .25 || _capturePending) return;
        _elapsed = 0;
        try { RefreshPage(); }
        catch (Exception exception) { StopRendering(exception); }
    }

    public override void _ExitTree()
    {
        _capturePending = false;
        DisconnectDrawSignal();
        // The scene tree owns the viewport and its controls. Do not Dispose the
        // shared portrait texture: it can still be referenced by cards at shutdown.
        _pageViewport = null;
        _portraitTexture = null;
        _originalPixels = null;
        // A started CPU task holds only managed pixel arrays. Let it finish;
        // there is no continuation that can touch this departing node.
        _compositionTask = null;
    }

    private void RefreshPage()
    {
        NararaShibaThreadPageSnapshot snapshot = NararaShibaThreadPageSync.Current;
        if (snapshot.Revision == _renderedRevision) return;
        // Preserve the original artwork while the one startup request is in
        // flight. The feed does not refresh on card draw, turn change or a timer.
        if (snapshot.Status is NararaShibaThreadPageStatus.Idle or NararaShibaThreadPageStatus.Loading) return;
        _renderedRevision = snapshot.Revision;
        _title.Text = string.IsNullOrWhiteSpace(snapshot.ThreadTitle) ? "NGA · 楼主更新" : snapshot.ThreadTitle;
        bool hasPosts = snapshot.Posts.Count > 0;
        int first = Math.Max(0, snapshot.Posts.Count - MaximumDisplayedPosts);
        int shown = snapshot.Posts.Count - first;
        _pageStatus.Text = hasPosts
            ? $"只看楼主 · {snapshot.AuthorName} · 第 {snapshot.PageNumber} / {snapshot.TotalPages} 页"
            : "只看楼主 · 当前无法取得页面";
        for (int slot = 0; slot < MaximumDisplayedPosts; slot++)
        {
            int index = first + slot;
            bool visible = index < snapshot.Posts.Count;
            _postHeaders[slot].Visible = visible;
            _postBodies[slot].Visible = visible;
            if (!visible) continue;
            NararaShibaThreadPost post = snapshot.Posts[index];
            _postHeaders[slot].Text = $"{snapshot.AuthorName}  ·  {post.PostedAt}  ·  楼主发言 {post.AuthorPostIndex}";
            // Label text is literal: forum markup/links can never create UI,
            // trigger requests, run commands, or become executable game content.
            string body = post.PlainText;
            if (post.ImageUrls.Count > 0) body += $"  [附图 {post.ImageUrls.Count} 张]";
            _postBodies[slot].Text = string.IsNullOrWhiteSpace(body) ? "（本条无可显示文字）" : body;
        }
        if (!hasPosts)
        {
            _postHeaders[0].Visible = true;
            _postBodies[0].Visible = true;
            _postHeaders[0].Text = "页面暂不可用";
            _postBodies[0].Text = "本次进入游戏未能取得楼主页面。\n下次启动游戏时会再次尝试。";
        }
        string freshness = snapshot.Status == NararaShibaThreadPageStatus.Stale ? "缓存" : "本次启动";
        string updated = snapshot.UpdatedAtUtc?.ToLocalTime().ToString("MM-dd HH:mm") ?? "未更新";
        _footer.Text = hasPosts
            ? $"{freshness} {updated} · 本页 {snapshot.Posts.Count} 条 / 显示末尾 {shown} 条文字节选 · 文字镜像"
            : "NGA 文字镜像 · 每次进入游戏更新";
        _capturePending = true;
        _pageViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    private void CapturePage()
    {
        if (!_capturePending || !IsInsideTree() || !GodotObject.IsInstanceValid(_pageViewport) ||
            !GodotObject.IsInstanceValid(_portraitTexture)) return;
        _capturePending = false;
        try
        {
            // FramePostDraw guarantees that the one-shot viewport render has
            // finished before readback. No viewport is deleted during that draw.
            using Image page = _pageViewport.GetTexture().GetImage();
            if (page.IsCompressed()) page.Decompress();
            page.Convert(Image.Format.Rgba8);
            byte[] original = _originalPixels, pixels = page.GetData();
            int width = page.GetWidth(), height = page.GetHeight();
            // Project only plain managed arrays in the worker. Capturing local
            // values avoids retaining a Godot node/resource in that task.
            _compositionTask = Task.Run(() => OverworkScreenCompositor.Compose(original, pixels, width, height));
            DisconnectDrawSignal();
        }
        catch (Exception exception)
        {
            StopRendering(exception);
        }
    }

    private void ApplyComposition()
    {
        try
        {
            // _Process observes completion; no background continuation invokes
            // Godot, and a card/scene disappearing during work cannot be resumed.
            byte[] composed = _compositionTask.GetAwaiter().GetResult();
            _compositionTask = null;
            using Image portrait = Image.CreateFromData(OverworkScreenCompositor.PortraitWidth,
                OverworkScreenCompositor.PortraitHeight, false, Image.Format.Rgba8, composed);
            _portraitTexture.Update(portrait);
            // The feed publishes one terminal result per game process. Leave
            // the viewport disabled and stop observing it after that capture.
            SetProcess(false);
            DisconnectDrawSignal();
        }
        catch (Exception exception)
        {
            StopRendering(exception);
        }
    }

    private void BuildPage()
    {
        _pageViewport = new SubViewport
        {
            Name = "OverworkNgaPage", Size = new Vector2I(PageWidth, PageHeight),
            Disable3D = true, GuiDisableInput = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
        };
        AddChild(_pageViewport);
        _font = new SystemFont { FontNames = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "sans-serif" } };
        Color paper = new("e8e0c8"), ink = new("38362e"), muted = new("756b55");
        AddBlock(new Rect2(0, 0, PageWidth, PageHeight), paper);
        AddBlock(new Rect2(0, 0, PageWidth, 58), new Color("534734"));
        AddLabel(new Rect2(24, 7, 300, 45), "NGA  玩家社区", 31, new Color("f5e9ca"));
        AddLabel(new Rect2(659, 13, 343, 34), "只看楼主   最后一页", 24, new Color("ddd0ae"));
        _title = AddLabel(new Rect2(24, 70, 976, 42), "", 27, ink);
        _pageStatus = AddLabel(new Rect2(24, 111, 976, 30), "", 22, muted);
        for (int slot = 0; slot < MaximumDisplayedPosts; slot++)
        {
            int y = 152 + slot * 132;
            AddBlock(new Rect2(18, y, 988, 123), new Color(slot % 2 == 0 ? "f4edda" : "ece4ce"));
            _postHeaders.Add(AddLabel(new Rect2(32, y + 5, 956, 30), "", 21, new Color("715b35")));
            Label body = AddLabel(new Rect2(32, y + 36, 956, 81), "", 24, ink);
            body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.MaxLinesVisible = 3;
            _postBodies.Add(body);
        }
        AddBlock(new Rect2(0, 685, PageWidth, 35), new Color("d4c9ad"));
        _footer = AddLabel(new Rect2(20, 687, 986, 29), "", 19, muted);
    }

    private void AddBlock(Rect2 bounds, Color color)
    {
        _pageViewport.AddChild(new ColorRect
        {
            Position = bounds.Position, Size = bounds.Size, Color = color,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
    }

    private Label AddLabel(Rect2 bounds, string text, int fontSize, Color color)
    {
        var label = new Label
        {
            Position = bounds.Position, Size = bounds.Size, Text = text,
            ClipText = true, MouseFilter = Control.MouseFilterEnum.Ignore,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        _pageViewport.AddChild(label);
        return label;
    }

    private void StopRendering(Exception exception)
    {
        _capturePending = false;
        SetProcess(false);
        DisconnectDrawSignal();
        GD.PushWarning($"Overwork page render stopped; keeping the existing portrait: {exception.Message}");
    }

    private void DisconnectDrawSignal()
    {
        if (!_drawSignalConnected) return;
        if (RenderingServer.Singleton.IsConnected(RenderingServer.SignalName.FramePostDraw, _afterDraw))
            RenderingServer.Singleton.Disconnect(RenderingServer.SignalName.FramePostDraw, _afterDraw);
        _drawSignalConnected = false;
    }
}
