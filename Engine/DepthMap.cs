// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/

using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Engine;

public class DepthMap : BitmapType
{
    public DepthMap(int resX, int resY)
    {
        // Create a flat monotone depthmap
        Bitmap = new WriteableBitmap(resX, resY, 96, 96, PixelFormats.Gray2, null);
    }

    public DepthMap(BitmapSource source)
    {
        // Always convert to greyscale
        Bitmap = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0.0);
    }

    public BitmapSource GetToScale(int resolutionX, int resolutionY)
    {
        var scale = new ScaleTransform((double)resolutionX / PixelWidth, (double)resolutionY / PixelHeight, PixelWidth / 2, PixelHeight / 2);
        return new TransformedBitmap(Bitmap, scale);
    }

    public BitmapSource GetLevelAdjusted(LevelAdjustments opt)
    {
        // Well, this is retarded... can manipulate the pixel data but not the palette data?
        var wb = new WriteableBitmap(Bitmap);
        var pixels = new byte[wb.PixelWidth * wb.PixelHeight];
        wb.CopyPixels(pixels, wb.PixelWidth, 0);

        var deltain = opt.Whitein - opt.Blackin;
        var deltaout = opt.Whiteout - opt.Blackout;

        for (var i = 0; i < wb.PixelWidth * wb.PixelHeight; i++)
        {
            var src = (double)pixels[i] / 255;
            // Set black and white points
            var dst = Math.Min(Math.Max(0, src - opt.Blackin), deltain);
            if (dst > 0.0 || !opt.UseHardBlack)
            {
                // Re-normalize
                dst /= deltain;
                // Apply gamma
                dst = Math.Pow(dst, opt.Gamma);
                // Scale to output range
                dst = opt.Blackout + (dst * deltaout);
            }
            pixels[i] = (byte)(dst * 255);
        }

        wb.WritePixels(new System.Windows.Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight), pixels, wb.PixelWidth, 0);
        return wb;
    }

    /// <summary>
    /// Return a bitmap with inverted levels
    /// </summary>
    /// <returns></returns>
    public BitmapSource GetLevelInverted()
    {
        // Well, this is retarded... can manipulate the pixel data but not the palette data?
        var wb = new WriteableBitmap(Bitmap);
        var pixels = new byte[wb.PixelWidth * wb.PixelHeight];
        wb.CopyPixels(pixels, wb.PixelWidth, 0);
        for (var i = 0; i < wb.PixelWidth * wb.PixelHeight; i++)
        {
            pixels[i] = (byte)(255 - pixels[i]);
        }
        wb.WritePixels(new System.Windows.Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight), pixels, wb.PixelWidth, 0);
        return wb;
    }

    /// <summary>
    /// Get a new depthmap which is the Z-depth max combination of the source and input depthmaps
    /// </summary>
    /// <param name="another"></param>
    /// <returns></returns>
    public DepthMap MergeWith(DepthMap another)
    {
        // First bitmap
        var wb = new WriteableBitmap(Bitmap);
        var pixels = new byte[wb.PixelWidth * wb.PixelHeight];
        wb.CopyPixels(pixels, wb.PixelWidth, 0);

        // Second bitmap, scaled to match the first (really should take the biggest one I guess)
        var bm2 = another.GetToScale(PixelWidth, PixelHeight);
        var pixels2 = new byte[bm2.PixelWidth * bm2.PixelHeight];
        bm2.CopyPixels(pixels2, bm2.PixelWidth, 0);
        for (var i = 0; i < wb.PixelWidth * wb.PixelHeight; i++)
        {
            if (pixels2[i] > pixels[i])
            {
                pixels[i] = pixels2[i];
            }
        }
        wb.WritePixels(new System.Windows.Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight), pixels, wb.PixelWidth, 0);
        return new DepthMap(wb);
    }

}

public struct LevelAdjustments
{
    public double Blackin;
    public double Whitein;
    public double Blackout;
    public double Whiteout;
    public double Gamma;
    public bool UseHardBlack;

    public LevelAdjustments(double blackin = 0.0, double whitein = 1.0, double blackout = 0.0, double whiteout = 1.0, double gamma = 1.0, bool useHardBlack = true)
    {
        this.Blackin = blackin;
        this.Whitein = whitein;
        this.Blackout = blackout;
        this.Whiteout = whiteout;
        this.Gamma = gamma;
        this.UseHardBlack = useHardBlack;
    }
}