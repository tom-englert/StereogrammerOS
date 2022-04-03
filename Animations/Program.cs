using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = SixLabors.ImageSharp.Color;

const int width = 1920;
const int height = 1080;

using Image<Rgba32> gif = new(width, height, Color.Blue);

// var texture = GenerateTexture((int)options.Separation, (int)options.Separation);
var texture = Image.Load(@"blue.jpg");

var options = new Parameters(texture)
{
    ImageWidth = width,
    ImageHeight = height,
    DepthFactor = .3f
};

for (var alpha = 0.0; alpha < 2 * Math.PI; alpha += Math.PI / 10)
{
    var depthMap = Tools.GenerateSinusDepthMap(width, height, alpha);

    var image = StereogramGenerator.Generate(depthMap, options);

    image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 5;

    //image.SaveAsPng(@"c:\temp\test.png");
    //return;

    gif.Frames.AddFrame(image.Frames.RootFrame);
}

gif.Frames.RemoveFrame(0);
gif.Metadata.GetGifMetadata().RepeatCount = 0;

var folder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

while (!folder.EnumerateDirectories("docs", SearchOption.TopDirectoryOnly).Any())
{
    folder = folder.Parent ?? throw new InvalidOperationException("Target folder not found!");
}

var fileName = Path.Combine(folder.FullName, "docs", "SineWave.gif");

gif.SaveAsGif(fileName);


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
                var p = random.Next(256);
                row[x] = new Rgb24((byte)(0.5 * p), (byte)(0.5 * p), (byte)p);

                // row[x] = new Rgb24((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            }
        }
    });

    return pixels;
}