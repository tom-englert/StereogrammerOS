namespace Engine
{
    public static class Tools
    {
        public static Image GenerateSinusDepthMap(int width, int height, double alpha)
        {
            var image = new Image<L8>(width, height);

            var centerX = width / 2;
            var centerY = height / 2;

            var factor = width / 20.0;

            var maxDist = 3.0 * Math.Sqrt(centerX * centerX + centerY * centerY);

            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);

                    for (var x = 0; x < width; x++)
                    {
                        var xi = x - centerX;
                        var yi = y - centerY;

                        var dist = Math.Sqrt(xi * xi + yi * yi);
                        var z = (byte)(128 * (Math.Sin(dist / factor + alpha) * (1.0 - dist / maxDist) + 1.0));

                        row[x] = new L8(z);
                    }
                }
            });

            return image;
        }

        public static Image GenerateBoxDepthMap(int width, int height, int cutOff)
        {
            var image = new Image<L8>(width, height);

            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    var z = y < cutOff ? 0 : (int)(((double)y - cutOff) / (height - cutOff) * 255.0);

                    var row = accessor.GetRowSpan(y);

                    row.Fill(new L8((byte)z));
                }
            });

            return image;
        }

        public static Image GenerateSinusShadowMap(int width, int height, double alpha)
        {
            var image = new Image<Rgba32>(width, height);

            var centerX = width / 2;
            var centerY = height / 2;

            var factor = width / 20.0;

            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);

                    for (var x = 0; x < width; x++)
                    {
                        var xi = x - centerX;
                        var yi = y - centerY;

                        var dist = Math.Sqrt(xi * xi + yi * yi);

                        var z = Math.Sin(dist / factor + alpha);
                        var slope = Math.Cos(dist / factor + alpha);
                        var phi = Math.Atan((double)yi / Math.Abs(xi)) / Math.PI;

                        var d = slope * phi;

                        var color = (byte)0; // xFF; // phi < 0 ? (byte)0xFF : (byte)0;

                        if (double.IsNaN(d))
                        {
                            row[x] = new Rgba32(color, color, color, 256);
                            continue;
                        }

                        var a = (byte)((d + 0.5) * 256);

                        row[x] = new Rgba32(color, color, color, a);
                    }
                }
            });

            return image;
        }

        public static IList<T[]> GetPixels<T>(this Image image, int width, int height)
            where T : unmanaged, IPixel<T>
        {
            var pixels = new List<T[]>(height);

            image
                .CloneAs<T>()
                .Clone(op => op.Resize(width, height))
                .ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < accessor.Height; y++)
                    {
                        var rowSpan = accessor.GetRowSpan(y);

                        pixels.Add(rowSpan.ToArray());
                    }
                });

            return pixels;
        }
    }
}
