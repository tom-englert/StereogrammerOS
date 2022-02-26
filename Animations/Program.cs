using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = SixLabors.ImageSharp.Color;

const int width = 1600;
const int height = 1200;

var options = new Options
{
    ResolutionX = width,
    ResolutionY = height,
    Separation = 128,
    FieldDepth = .5
};

using Image<Rgba32> gif = new(width, height, Color.Blue);

// var texture = GenerateTexture((int)options.Separation, (int)options.Separation);
var texture = Image.Load(@"NuclearCoral.jpg");
var generator = StereogramGenerator.Get(options);

for (var alpha = 0.0; alpha < 2 * Math.PI; alpha += Math.PI / 10)
{
    var depthMap = GenerateDepthMap(width, height, alpha);

    var image = generator.Generate(depthMap, texture);

    image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 5;

    //image.SaveAsPng(@"c:\temp\test.png");
    //return;

    gif.Frames.AddFrame(image.Frames.RootFrame);
}

gif.Frames.RemoveFrame(0);
gif.Metadata.GetGifMetadata().RepeatCount = 0;

var folder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

while (!folder.EnumerateDirectories("doc", SearchOption.TopDirectoryOnly).Any())
{
    folder = folder.Parent ?? throw new InvalidOperationException("Target folder not found!");
}

var fileName = Path.Combine(folder.FullName, "doc", "SineWave.gif");

gif.SaveAsGif(fileName);


static Image GenerateDepthMap(int resX, int resY, double alpha)
{
    var pixels = new Image<A8>(resX, resY);

    var centerX = resX / 2;
    var centerY = resY / 2;

    var factor = resX / 20.0;

    var maxDist = Math.Sqrt(centerX * centerX + centerY * centerY);

    pixels.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < resY; y++)
        {
            var row = accessor.GetRowSpan(y);

            for (var x = 0; x < resX; x++)
            {
                var dist = Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                var z = (byte)(128 * (Math.Sin(dist / factor + alpha) * (1.0 - dist/maxDist) + 1.0));

                row[x] = new A8(z);
            }
        }
    });

    return pixels;
}

static Image GenerateTexture(int resX, int resY)
{
    var random = new Random();
    var pixels = new Image<Rgb24>(resX, resY);

    pixels.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < resY; y++)
        {
            var row = accessor.GetRowSpan(y);

            for (var x = 0; x < resX; x++)
            {
                var p = (byte)random.Next(256);
                row[x] = new Rgb24(p, p, p);

                // row[x] = new Rgb24((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            }
        }
    });

    return pixels;
}