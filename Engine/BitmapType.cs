// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/

using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Engine;

public enum FileType { Jpg, Bmp, Png };

public class BitmapType
{
    private BitmapSource _bitmap = null;

    public BitmapSource Bitmap
    {
        get => _bitmap;
        protected set
        {
            _bitmap = value;
            _bitmap.Freeze();
        }
    }

    public string Name { get; set; }
    
    public string FileName { get; set; }
    
    public int PixelWidth => _bitmap?.PixelWidth ?? 0;
    
    public int PixelHeight => _bitmap?.PixelHeight ?? 0;

    public int Width => PixelWidth;
    
    public int Height => PixelHeight;

    public bool CanRemove { get; set; } = true;

    public virtual BitmapSource GetThumbnail(bool bSelected)
    {
        return Bitmap;
    }

    public override string ToString()
    {
        return Name;
    }

    public void SaveImage(string filename, FileType type)
    {
        Name = Path.GetFileNameWithoutExtension(filename);
        FileName = filename;

        BitmapEncoder encoder = type switch
        {
            FileType.Jpg => new JpegBitmapEncoder() {QualityLevel = 100},
            FileType.Bmp => new BmpBitmapEncoder(),
            FileType.Png => new PngBitmapEncoder(),
            _ => throw new Exception("Error saving file")
        };

        BitmapSource bitmap = new FormatConvertedBitmap(Bitmap, PixelFormats.Bgra32, null, 0.0f);

        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new FileStream(filename, FileMode.Create);
        
        encoder.Save(stream);
    }

    /// <summary>
    /// Return a Bitmap resized to the specified resolution and converted to the specified pixel format
    /// </summary>
    /// <param name="resolutionX"></param>
    /// <param name="resolutionY"></param>
    /// <param name="format"></param>
    /// <returns></returns>
    public BitmapSource GetToScaleAndFormat(int resolutionX, int resolutionY, PixelFormat format)
    {
        var fmTexture = new FormatConvertedBitmap(Bitmap, format, null, 0.0);
        if (fmTexture.PixelWidth == resolutionX && fmTexture.PixelHeight == resolutionY)
        {
            return fmTexture;
        }
        else
        {
            var scale = new ScaleTransform((double)resolutionX / PixelWidth, (double)resolutionY / PixelHeight, PixelWidth / 2.0, PixelHeight / 2.0);
            return new TransformedBitmap(fmTexture, scale);
        }
    }
}