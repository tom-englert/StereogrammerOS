using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace EngineTest;

[UsesVerify]
public class Test
{
    static Test()
    {
        VerifyImageSharp.Initialize();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task EnsureOutputDoesNotChange(double alpha)
    {
        const int width = 1920;
        const int height = 1080;

        var texture = await Image.LoadAsync(@"Blue.jpg");

        var parameters = new Parameters(texture)
        {
            ImageWidth = width,
            ImageHeight = height,
            DepthFactor = .3f
        };

        // var depthMap = await Image.LoadAsync(@"D:\Develop\My Open Source\GitHub\StereogrammerOS\Stereogrammer\Images\3D2.png");

        var depthMap = Tools.GenerateSinusDepthMap(width, height, alpha);

        var image = StereogramGenerator.Generate(depthMap, parameters);

//     image.Mutate(op => op.DrawImage(shadow, new Point(-parameters.Separation/2), 0.5f).DrawImage(shadow, new Point(parameters.Separation / 2), 0.5f));

        await Verify(image).UseParameters(alpha); // .AutoVerify();
    }

    [Theory]
    [InlineData(0.0)]
    // [InlineData(1.0)]
    // [InlineData(2.0)]
    public async Task TestShadowMap(double alpha)
    {
        var image = Tools.GenerateSinusShadowMap(128, 128, alpha);

        await Verify(image).UseParameters(alpha); // .AutoVerify();
    }


    [Theory]
    [InlineData(20, 20, 45.0)]
    [InlineData(20, 0, 0.0)]
    [InlineData(20, -20, -45.0)]
    [InlineData(0, 20, 90.0)]
    [InlineData(0, 0, double.NaN)]
    [InlineData(0, -20, -90.0)]
    [InlineData(-20, 20, -45.0)]
    [InlineData(-20, 1, -2.86)]
    [InlineData(-20, 0, 0.0)]
    [InlineData(-20, -1, 2.86)]
    [InlineData(-20, -20, 45.0)]
    public async Task ArcTan(int x, int y, double expected)
    {
        var target = Math.Atan((double)y / x) * 360.0 / (2.0 * Math.PI);

        Assert.Equal(expected, target, 2);
    }
}
