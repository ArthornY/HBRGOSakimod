using System;

namespace HBRGOSakiMod.Core.Helpers;

// Only the monitor's inner display is replaced. Keep the user's original file and
// every pixel outside the display unchanged, including its frame and the character.
internal static class OverworkScreenCompositor
{
    internal const int PortraitWidth = 1448;
    internal const int PortraitHeight = 1086;
    private static readonly double[] Projection = BuildInverseProjection();

    internal static byte[] Compose(byte[] portrait, byte[] page, int pageWidth, int pageHeight)
    {
        if (portrait.Length != PortraitWidth * PortraitHeight * 4)
            throw new ArgumentException("Unexpected Overwork portrait dimensions.", nameof(portrait));
        if (pageWidth <= 0 || pageHeight <= 0 || page.Length != checked(pageWidth * pageHeight * 4))
            throw new ArgumentException("Invalid page image dimensions.", nameof(page));

        byte[] result = (byte[])portrait.Clone();
        for (int y = 65; y <= 352; y++)
        for (int x = 106; x <= 529; x++)
        {
            if (!TryGetMonitorUv(x + .5, y + .5, out double u, out double v)) continue;
            double sourceX = u * (pageWidth - 1), sourceY = v * (pageHeight - 1);
            int x0 = (int)sourceX, y0 = (int)sourceY;
            int x1 = Math.Min(x0 + 1, pageWidth - 1), y1 = Math.Min(y0 + 1, pageHeight - 1);
            double fx = sourceX - x0, fy = sourceY - y0;
            int target = (y * PortraitWidth + x) * 4;
            for (int channel = 0; channel < 3; channel++)
            {
                double top = Sample(x0, y0, channel) * (1 - fx) + Sample(x1, y0, channel) * fx;
                double bottom = Sample(x0, y1, channel) * (1 - fx) + Sample(x1, y1, channel) * fx;
                result[target + channel] = (byte)Math.Clamp((int)Math.Round(top * (1 - fy) + bottom * fy), 0, 255);
            }
            result[target + 3] = 255;
        }
        return result;

        byte Sample(int x, int y, int channel) => page[(y * pageWidth + x) * 4 + channel];
    }

    internal static bool TryGetMonitorUv(double x, double y, out double u, out double v)
    {
        double divisor = Projection[6] * x + Projection[7] * y + 1;
        u = (Projection[0] * x + Projection[1] * y + Projection[2]) / divisor;
        v = (Projection[3] * x + Projection[4] * y + Projection[5]) / divisor;
        return u >= 0 && u <= 1 && v >= 0 && v <= 1;
    }

    private static double[] BuildInverseProjection()
    {
        // Measured from the unmodified 1448 x 1086 source: TL, TR, BR, BL.
        (double X, double Y, double U, double V)[] corners =
        {
            (106, 65, 0, 0), (513, 108, 1, 0),
            (529, 352, 1, 1), (122, 348, 0, 1)
        };
        var matrix = new double[8, 9];
        for (int index = 0; index < corners.Length; index++)
        {
            var (x, y, u, v) = corners[index];
            int row = index * 2;
            matrix[row, 0] = x; matrix[row, 1] = y; matrix[row, 2] = 1;
            matrix[row, 6] = -u * x; matrix[row, 7] = -u * y; matrix[row, 8] = u;
            matrix[row + 1, 3] = x; matrix[row + 1, 4] = y; matrix[row + 1, 5] = 1;
            matrix[row + 1, 6] = -v * x; matrix[row + 1, 7] = -v * y; matrix[row + 1, 8] = v;
        }
        for (int column = 0; column < 8; column++)
        {
            int pivot = column;
            for (int row = column + 1; row < 8; row++)
                if (Math.Abs(matrix[row, column]) > Math.Abs(matrix[pivot, column])) pivot = row;
            if (Math.Abs(matrix[pivot, column]) < 1e-10)
                throw new InvalidOperationException("The monitor projection is degenerate.");
            for (int item = column; item < 9; item++)
                (matrix[column, item], matrix[pivot, item]) = (matrix[pivot, item], matrix[column, item]);
            double scale = matrix[column, column];
            for (int item = column; item < 9; item++) matrix[column, item] /= scale;
            for (int row = 0; row < 8; row++)
            {
                if (row == column) continue;
                double factor = matrix[row, column];
                for (int item = column; item < 9; item++) matrix[row, item] -= factor * matrix[column, item];
            }
        }
        var projection = new double[8];
        for (int index = 0; index < 8; index++) projection[index] = matrix[index, 8];
        return projection;
    }
}
