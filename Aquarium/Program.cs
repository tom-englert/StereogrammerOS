using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;

const int width = 1920;
const int height = 1080;
const float depthFactor = .3f;

var cutOff = (height * 2) / 3;

var texture1 = Image.Load(@"blue.jpg");
var texture2 = Image.Load(@"brown.jpg");

var texture = new Image<Rgba32>(128, height);

for (var y = 0; y < cutOff; y += texture1.Height)
{
    texture.Mutate(op => op.DrawImage(texture1, new Point(0, y), 1f));
}

for (var y = cutOff; y < height; y += texture2.Height)
{
    texture.Mutate(op => op.DrawImage(texture2, new Point(0, y), 1f));
}

var options = new Parameters(texture)
{
    ImageWidth = width,
    ImageHeight = height,
    DepthFactor = depthFactor
};

var depthMap = Tools.GenerateBoxDepthMap(width, height, cutOff);

var image = StereogramGenerator.Generate(depthMap, options);

FontCollection collection = new();
var family = collection.Add(@"c:\windows\fonts/arial.ttf");
var font = family.CreateFont(24, FontStyle.Bold);

void Draw(int x, int y, float depth)
{
    const int p = 2;
    var s = StereogramGenerator.GetSeparation(options.Separation, depth * depthFactor) / p;
    image.Mutate(op => op.DrawText("O", font, Color.Black, new PointF(x, y)));
    image.Mutate(op => op.DrawText("O", font, Color.Black, new PointF(x + s, y)));
    image.Mutate(op => op.DrawText("O", font, Color.Black, new PointF(x + 2 * s, y)));
}

Draw(900, 400, .5f);
Draw(900, 500, .1f);
Draw(900, 600, .7f);

const float p = 2f;
var s = (int)(StereogramGenerator.GetSeparation(options.Separation, .5f * depthFactor) / p);
for (var i = 0; i < width; i += s)
{
    image.Mutate(op => op.DrawText("O", font, Color.Black, new PointF(i, 700)));
}

/*
s = StereogramGenerator.GetSeparation(options.Separation, .1f * depthFactor) / p;
for (var i = 0; i < width; i += s)
{
    image.Mutate(op => op.DrawText("O", font, Color.Black, new PointF(i, 700)));
}
s = StereogramGenerator.GetSeparation(options.Separation, .8f * depthFactor) / p;
for (var i = 0; i < width; i += s)
{
    image.Mutate(op => op.DrawText("O", font, Color.Black, new PointF(i, 900)));
}
*/

image.SaveAsPng(@"c:\temp\test.png");

