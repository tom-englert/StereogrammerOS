// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/
//
// See individual classes for algorithm copyrights

namespace Engine;

public static class StereogramGenerator
{
    public static Image<Rgba32> Generate(IList<L8[]> depthBytes, Parameters parameters)
    {
        var imageWidth = parameters.ImageWidth;
        var imageHeight = parameters.ImageHeight;

        var image = new Image<Rgba32>(imageWidth, imageHeight);
        var texture = parameters.Texture;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                var depthRow = depthBytes[y];
                var textureRow = texture[y % texture.Count];

                CreateRow(pixelRow, depthRow, textureRow, parameters.Separation, Math.Clamp(parameters.DepthFactor, 0.0f, 1.0f));
            }
        });

        return image;
    }

    public static Image<Rgba32> Generate(Image depthMap, Parameters parameters)
    {
        return Generate(depthMap.GetPixels<L8>(parameters.ImageWidth, parameters.ImageHeight), parameters);
    }

    private static void CreateRow(Span<Rgba32> imageRow, IReadOnlyList<L8> depthsRow, IReadOnlyList<Rgba32> textureRow, int separation, float depthFactor)
    {
        var imageWidth = depthsRow.Count;

        var constraints = GetConstraints(depthsRow, separation, depthFactor);

        for (var x = 0; x < imageWidth; x++)
        {
            var constraint = x;

            int c;
            while ((c = constraints[constraint]) != -1)
            {
                constraint = c;
            }

            imageRow[x] = textureRow[constraint % textureRow.Count];
        }
    }

    private static int[] GetConstraints(IReadOnlyList<L8> depthsRow, int separation, float depthFactor)
    {
        var width = depthsRow.Count;
        var center = width / 2;

        var constraints = Enumerable.Repeat(-1, width).ToArray();

        (int left, int right) GetPair(int x)
        {
            var depth = depthsRow[x];
            var s = GetSeparation(separation, depthFactor * depth.PackedValue / 256f);
            var left = x - (s / 2);
            var right = x + s;
            return (left, right);
        }

        var pairs = Enumerable.Range(0, width).Select(GetPair).ToArray();

        var leftPairs = pairs.Take(center).Reverse();

        void FillLeft(int limit, (int, int) last)
        {
            var (left, right) = last;
            while (left > limit)
            {
                constraints[left--] = right--;
            }

        }

        void FillRight(int limit, (int, int) last)
        {
            var (left, right) = last;
            while (right < limit)
            {
                constraints[right++] = left++;
            }
        }

        var last = (int.MinValue, int.MinValue);

        foreach (var (left, right) in leftPairs)
        {
            FillLeft(Math.Max(left, -1), last);

            last = (left, right);
        }

        FillLeft(-1, last);

        var rightPairs = pairs.Skip(center);

        last = (int.MaxValue, int.MaxValue);

        foreach (var (left, right) in rightPairs)
        {
            FillRight(Math.Min(right, width), last);

            last = (left, right);
        }

        FillRight(width, last);

        return constraints;
    }

    public static int GetSeparation(double separation, float depth)
    {
        return (int)Math.Round((1 - depth) * 2 * separation / (2 - depth));
    }
}
