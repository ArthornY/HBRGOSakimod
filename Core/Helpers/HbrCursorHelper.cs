using System;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrCursorHelper
{
    internal const string ImagePath = "res://images/ui/cursor/hbr_cursor.png";
    // The original 32px cursor uses (6, 5); keep the same tip at native 64px size.
    private static readonly Vector2 OriginalHotSpot = new(12f, 10f);
    private const int PressFrameCount = 8;
    private static readonly Image[] PressFrames = new Image[PressFrameCount + 1];
    private static int _pressFrame;
    internal static Vector2 HotSpot => OriginalHotSpot * CurrentScale;
    private static float CurrentScale => 1f - _pressFrame / (float)PressFrameCount * 0.25f;
    private static Image _image;
    private static bool _reportedLoadFailure;

    internal static Image GetImage()
    {
        if (GodotObject.IsInstanceValid(_image)) return GetCurrentFrame();
        try
        {
            if (!ResourceLoader.Exists(ImagePath))
            {
                ReportLoadFailure("image resource is missing");
                return null;
            }

            Texture2D texture = ResourceLoader.Load<Texture2D>(ImagePath);
            Image image = texture?.GetImage();
            if (image == null || image.IsEmpty() ||
                (image.IsCompressed() && image.Decompress() != Error.Ok))
            {
                ReportLoadFailure("image could not be decoded");
                return null;
            }

            image.Convert(Image.Format.Rgba8);
            // Keep the Image alive: the native cursor manager caches it by identity.
            _image = image;
            return GetCurrentFrame();
        }
        catch (Exception exception)
        {
            ReportLoadFailure(exception.Message);
            return null;
        }
    }

    internal static bool SetPressAmount(float amount)
    {
        int frame = (int)MathF.Round(Math.Clamp(amount, 0f, 1f) * PressFrameCount);
        if (frame == _pressFrame) return false;
        _pressFrame = frame;
        return true;
    }

    private static Image GetCurrentFrame()
    {
        if (_pressFrame == 0) return _image;
        if (GodotObject.IsInstanceValid(PressFrames[_pressFrame])) return PressFrames[_pressFrame];
        // Cache hardware-cursor frames once; scaling the hotspot keeps the click tip fixed.
        Image frame = (Image)_image.Duplicate();
        frame.Resize((int)MathF.Round(_image.GetWidth() * CurrentScale),
            (int)MathF.Round(_image.GetHeight() * CurrentScale), Image.Interpolation.Lanczos);
        PressFrames[_pressFrame] = frame;
        return frame;
    }

    internal static bool IsApplied(Image image) =>
        GodotObject.IsInstanceValid(_image) && GodotObject.IsInstanceValid(image) &&
        ReferenceEquals(image, _pressFrame == 0 ? _image : PressFrames[_pressFrame]);

    internal static bool IsOwnedImage(Image image)
    {
        if (!GodotObject.IsInstanceValid(image)) return false;
        if (ReferenceEquals(image, _image)) return true;
        foreach (Image frame in PressFrames)
            if (ReferenceEquals(image, frame)) return true;
        return false;
    }

    private static void ReportLoadFailure(string reason)
    {
        if (_reportedLoadFailure) return;
        _reportedLoadFailure = true;
        Log.Warn($"[HBRGOSakiMod] HBR cursor unavailable; keeping the native cursor: {reason}.");
    }
}
