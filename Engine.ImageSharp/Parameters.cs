namespace Engine;

public class Parameters
{
    public Parameters(Image textureImage, int separation = 128)
    {
        Separation = separation;
        Texture = textureImage.GetPixels<Rgba32>(separation, textureImage.Height); ;
    }

    public IList<Rgba32[]> Texture { get; }

    public int Separation { get; }

    public int ImageWidth { get; set; } = 1024;
    
    public int ImageHeight { get; set; } = 768;
   
    public float DepthFactor { get; set; } = 0.3333f;
}