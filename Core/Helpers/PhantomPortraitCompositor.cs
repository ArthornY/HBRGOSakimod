using System;

namespace HBRGOSakiMod.Core.Helpers;

// Fits a rendered monster into the phantom portrait: its opaque bounds centred at 88% of the frame, aspect kept.
// Compose lays it on white for the phantom card art; ComposeCutout keeps the renderer's alpha so the phantom
// roster can lay the same picture over its own dark backdrop.
internal static class PhantomPortraitCompositor
{
    internal const int Width = 512;
    internal const int Height = 384;

    internal static byte[] Compose(byte[] rgba, int width, int height)
    {
        Validate(rgba, width, height);
        byte[] output = new byte[Width * Height * 4];
        Array.Fill(output, (byte)255);
        if (!TryFrame(rgba, width, height, out Framing frame)) return output;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            frame.Locate(x, y, out int x0, out int y0, out double fx, out double fy);
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

    // The same framing and colours without the white: straight alpha, taken only from the renderer's own alpha, so
    // white fur and highlights on the monster stay opaque. Laid over white it reproduces Compose to within rounding.
    internal static byte[] ComposeCutout(byte[] rgba, int width, int height)
    {
        Validate(rgba, width, height);
        byte[] output = new byte[Width * Height * 4];
        if (!TryFrame(rgba, width, height, out Framing frame)) return output;
        double[] coverage = new double[Width * Height];
        double[] colour = new double[Width * Height * 3];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            frame.Locate(x, y, out int x0, out int y0, out double fx, out double fy);
            double w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
            int pixel = y * Width + x;
            double alpha = w00 * Alpha(x0, y0) + w10 * Alpha(x0 + 1, y0) +
                w01 * Alpha(x0, y0 + 1) + w11 * Alpha(x0 + 1, y0 + 1);
            coverage[pixel] = alpha;
            if (alpha <= 0) continue;
            for (int channel = 0; channel < 3; channel++)
            {
                // Premultiplied interpolation divided by the coverage: the alpha-weighted mean of the samples, so
                // the colour of invisible source pixels never reaches the silhouette.
                double premultiplied = w00 * Premultiplied(x0, y0, channel) + w10 * Premultiplied(x0 + 1, y0, channel) +
                    w01 * Premultiplied(x0, y0 + 1, channel) + w11 * Premultiplied(x0 + 1, y0 + 1, channel);
                colour[pixel * 3 + channel] = premultiplied * 255 / alpha;
            }
        }

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int pixel = y * Width + x, index = pixel * 4;
            if (coverage[pixel] <= 0) Bleed(x, y, pixel);
            for (int channel = 0; channel < 3; channel++)
                output[index + channel] = (byte)Math.Clamp((int)Math.Round(colour[pixel * 3 + channel]), 0, 255);
            output[index + 3] = (byte)Math.Clamp((int)Math.Round(coverage[pixel]), 0, 255);
        }
        return output;

        double Alpha(int x, int y) =>
            x < 0 || y < 0 || x >= width || y >= height ? 0 : rgba[(y * width + x) * 4 + 3];

        double Premultiplied(int x, int y, int channel)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return 0;
            int index = (y * width + x) * 4;
            return rgba[index + channel] * rgba[index + 3] / 255d;
        }

        // An empty pixel beside the silhouette takes its covered neighbours' colour, weighted by their coverage:
        // the GPU filters straight alpha, so the edge then fades into the backdrop instead of into black.
        void Bleed(int x, int y, int pixel)
        {
            double weight = 0, red = 0, green = 0, blue = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                int neighbour = ny * Width + nx;
                double share = coverage[neighbour];
                if (share <= 0) continue;
                weight += share;
                red += share * colour[neighbour * 3];
                green += share * colour[neighbour * 3 + 1];
                blue += share * colour[neighbour * 3 + 2];
            }
            if (weight <= 0) return;
            colour[pixel * 3] = red / weight;
            colour[pixel * 3 + 1] = green / weight;
            colour[pixel * 3 + 2] = blue / weight;
        }
    }

    private static void Validate(byte[] rgba, int width, int height)
    {
        if (width <= 0 || height <= 0 || rgba.Length != checked(width * height * 4))
            throw new ArgumentException("Invalid RGBA image dimensions.");
    }

    private static bool TryFrame(byte[] rgba, int width, int height, out Framing frame)
    {
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
        if (right < left)
        {
            frame = default;
            return false;
        }

        double scale = Math.Min(Width * 0.88 / (right - left + 1),
            Height * 0.88 / (bottom - top + 1));
        double offsetX = (Width - (right - left + 1) * scale) / 2;
        double offsetY = (Height - (bottom - top + 1) * scale) / 2;
        frame = new Framing(left, top, scale, offsetX, offsetY);
        return true;
    }

    // Maps a portrait pixel back to the bilinear sample position in the source.
    private readonly struct Framing
    {
        private readonly int _left, _top;
        private readonly double _scale, _offsetX, _offsetY;

        internal Framing(int left, int top, double scale, double offsetX, double offsetY)
        {
            _left = left;
            _top = top;
            _scale = scale;
            _offsetX = offsetX;
            _offsetY = offsetY;
        }

        internal void Locate(int x, int y, out int x0, out int y0, out double fx, out double fy)
        {
            double sx = _left + (x + 0.5 - _offsetX) / _scale - 0.5;
            double sy = _top + (y + 0.5 - _offsetY) / _scale - 0.5;
            x0 = (int)Math.Floor(sx);
            y0 = (int)Math.Floor(sy);
            fx = sx - x0;
            fy = sy - y0;
        }
    }
}
