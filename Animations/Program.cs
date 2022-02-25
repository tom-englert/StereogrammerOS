using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = SixLabors.ImageSharp.Color;

const int width = 1600;
const int height = 1200;

var options = new Options();

options.Texture = new Texture(TextureType.Colourdots, (int)options.Separation, (int)options.Separation);
options.ResolutionX = width;
options.ResolutionY = height;

using Image<Rgba32> gif = new(width, height, Color.Blue);

for (var alpha = 0.0; alpha < 2 * Math.PI; alpha += Math.PI / 10)
{
    var depthMap = GenerateDepthMap(width, height, alpha);

    options.DepthMap = depthMap;
    var generator = StereogramGenerator.Get(options);

    var bitmap = new BitmapType { Bitmap = generator.Generate() };

    using var bitmapStream = new MemoryStream();
    bitmap.SaveImage(bitmapStream, FileType.Bmp);
    bitmapStream.Position = 0;

    using var image = Image.Load(bitmapStream);
    image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 5;

    gif.Frames.AddFrame(image.Frames.RootFrame);
}

gif.Frames.RemoveFrame(0);
gif.Metadata.GetGifMetadata().RepeatCount = 0;
gif.SaveAsGif(@"c:\temp\output.gif");


static DepthMap GenerateDepthMap(int resX, int resY, double alpha)
{
    var bytesPerRow = ((resX + 3) >> 2) << 2; // Round up to multiple of 4
    var pixels = new byte[bytesPerRow * resY];

    var centerX = resX / 2;
    var centerY = resY / 2;

    var factor = resX / 20.0;

    for (var y = 0; y < resY; y++)
    {
        var rowOffset = y * bytesPerRow;

        for (var x = 0; x < resX; x++)
        {
            var dist = Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
            var z = (byte)(128 * (Math.Sin(dist / factor + alpha) + 1.0));
            pixels[rowOffset + x] = z;
        }
    }

    var bitmap = new WriteableBitmap(resX, resY, 96.0, 96.0, PixelFormats.Gray8, null);
    bitmap.WritePixels(new Int32Rect(0, 0, resX, resY), pixels, bytesPerRow, 0);
    return new DepthMap(bitmap);
}