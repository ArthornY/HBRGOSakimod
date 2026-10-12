using System;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomPortraitCompositor
{
    internal const int Width = 512;
    internal const int Height = 384;

    internal static byte[] Compose(byte[] rgba, int width, int height)
    {
        if (width <= 0 || height <= 0 || rgba.Length != checked(width * height * 4))
            throw new ArgumentException("Invalid RGBA image dimensions.");
        byte[] output = new byte[Width * Height * 4];
        Array.Fill(output, (byte)255);
        int left = width, top = height, right = -1, bottom = -1;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (rgba[(y * width + x) * 4 + 3] <= 16) continue;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }
        if (right < left) return output;

        double scale = Math.Min(Width * 0.88 / (right - left + 1),
            Height * 0.88 / (bottom - top + 1));
        double offsetX = (Width - (right - left + 1) * scale) / 2;
        double offsetY = (Height - (bottom - top + 1) * scale) / 2;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            double sx = left + (x + 0.5 - offsetX) / scale - 0.5;
            double sy = top + (y + 0.5 - offsetY) / scale - 0.5;
            int x0 = (int)Math.Floor(sx), y0 = (int)Math.Floor(sy);
            double fx = sx - x0, fy = sy - y0;
            int index = (y * Width + x) * 4;
            for (int channel = 0; channel < 3; channel++)
            {
                double value = Sample(x0, y0, channel) * (1 - fx) * (1 - fy) +
                    Sample(x0 + 1, y0, channel) * fx * (1 - fy) +
                    Sample(x0, y0 + 1, channel) * (1 - fx) * fy +
                    Sample(x0 + 1, y0 + 1, channel) * fx * fy;
                output[index + channel] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
            }
        }
        return output;

        double Sample(int x, int y, int channel)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return 255;
            int index = (y * width + x) * 4;
            // Composite before interpolation so invisible RGB cannot bleed into the edges.
            return 255 + (rgba[index + channel] - 255) * rgba[index + 3] / 255d;
        }
    }
}
