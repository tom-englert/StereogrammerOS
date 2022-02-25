// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Engine;

public class Texture : BitmapType
{
    public enum TextureType
    {
        Greydots,
        Colourdots,
        Bitmap
    }

    private readonly TextureType _type;

    // Create a texture for writing... nah, use the subclasses or bitmap constructor please
    protected Texture(TextureType type, int resX, int resY)
    {
        _type = type;

        Bitmap = type switch
        {
            TextureType.Greydots => GenerateRandomDots(resX, resY),
            TextureType.Colourdots => GenerateColoredDots(resX, resY),
            _ => throw new ArgumentException("invalid type", nameof(type))
        };
    }

    public Texture(BitmapSource source)
    {
        _type = TextureType.Bitmap;
        Bitmap = source;
    }

    /// <summary>
    /// Generate a monochromatic random dot texture (using 256 shades of grey)
    /// </summary>
    /// <param name="resX"></param>
    /// <param name="resY"></param>
    /// <returns></returns>
    public static BitmapSource GenerateRandomDots(int resX, int resY)
    {
        var random = new Random();

        var bytesPerRow = ((resX + 3) >> 2) << 2; // Round up to multiple of 4

        var pixels = new byte[bytesPerRow * resY];

        // Worth parallelization?
        for (var i = 0; i < bytesPerRow * resY; i++)
        {
            pixels[i] = (byte)random.Next(256);
        }

        var wb = new WriteableBitmap(resX, resY, 96.0, 96.0, PixelFormats.Gray8, null);
        wb.WritePixels(new Int32Rect(0, 0, resX, resY), pixels, bytesPerRow, 0);
        return wb;
    }

    /// <summary>
    /// Generate a colored random dot texture (using 8 bits per pixel RGB)
    /// </summary>
    /// <param name="resX"></param>
    /// <param name="resY"></param>
    /// <returns></returns>
    public static BitmapSource GenerateColoredDots(int resX, int resY)
    {
        var random = new Random();

        var bytesPerRow = resX * 3; // Rgb24

        var pixels = new byte[bytesPerRow * resY];

        // Worth parallelization?
        for (var i = 0; i < bytesPerRow * resY; i++)
        {
            pixels[i] = (byte)random.Next(256);
        }

        var wb = new WriteableBitmap(resX, resY, 96.0, 96.0, PixelFormats.Rgb24, null);
        wb.WritePixels(new Int32Rect(0, 0, resX, resY), pixels, bytesPerRow, 0);
        return wb;
    }
}