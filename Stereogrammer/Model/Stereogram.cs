// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/

using System.Windows.Media.Imaging;
using Engine;

namespace Stereogrammer.Model;

/// <summary>
/// Stereogram image.  A BitmapType which also stores the options it was generated from.
/// </summary>
public class Stereogram : BitmapType
{
    public Stereogram(BitmapSource bitmap)
    {
        Bitmap = bitmap;
    }

    public bool HasOptions => Options != null;

    /// <summary>
    /// Options used to generate the stereogram
    /// </summary>
    public Options Options { get; set; }

    /// <summary>
    /// Time taken to generate stereogram, in milliseconds
    /// </summary>
    public long Milliseconds = 0;
}