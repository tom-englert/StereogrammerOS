// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/
//
// See individual classes for algorithm copyrights

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using System.Threading.Tasks;

namespace Engine;

public enum Algorithm { Horoptic, Techmind, ConstraintSatisfaction, Lookback, TylerChang };

public enum Oversample { X1, X2, X3, X4, X6, X8 };

/// <summary>
/// Base class for stereogram generator - does most of the work, but lets subclasses provide
/// an implementation of a particular algorithm for processing each line of the image
/// </summary>
public abstract class StereogramGenerator
{
    /// <summary>
    /// Factory method to get a generator for the appropriate algorithm
    /// </summary>
    public static StereogramGenerator Get(Options options)
    {
        switch (options.Algorithm)
        {
            case Algorithm.Horoptic:
                return new StereogramGeneratorHoroptic(options);
            case Algorithm.TylerChang:
                return new StereogramGeneratorTylerChang(options);
            case Algorithm.ConstraintSatisfaction:
                return new StereogramGeneratorConstraintSatisfaction(options);
            case Algorithm.Lookback:
                return new StereogramGeneratorLookBack(options);
            case Algorithm.Techmind:
                return new StereogramGeneratorTechmind(options);
            default:
                throw new Exception("Unimplemented Algorithm!");
        }
    }

    /// <summary>
    /// Constructor caches the important options and does some common setup work
    /// </summary>
    public StereogramGenerator(Options options)
    {
        _options = new Options(options);

        _depthMap = options.DepthMap;
        _texture = options.Texture;
        resolutionX = options.ResolutionX;
        resolutionY = options.ResolutionY;
        separation = options.Separation;
        FieldDepth = options.FieldDepth;

        // Validate the options
        if (_depthMap == null)
            throw new ArgumentNullException("options.depthmap");
        if (_texture == null)
            throw new ArgumentNullException("options.texture");

        // Make sure we respect aspect ratio preservation
        if (options.PreserveAspectRatio)
        {
            double w = resolutionX;
            double h = resolutionY;

            var ratio = w / h;

            var bmRatio = (double)_depthMap.PixelWidth / _depthMap.PixelHeight;

            if (bmRatio < ratio)
            {
                resolutionY = (int)h;
                resolutionX = (int)(h * bmRatio);
            }
            else
            {
                resolutionX = (int)w;
                resolutionY = (int)(w / bmRatio);
            }
        }

        // Convert oversample enum to int
        switch (options.OverSample)
        {
            case Engine.Oversample.X1:
                Oversample = 1;
                break;
            case Engine.Oversample.X2:
                Oversample = 2;
                break;
            case Engine.Oversample.X3:
                Oversample = 3;
                break;
            case Engine.Oversample.X4:
                Oversample = 4;
                break;
            case Engine.Oversample.X6:
                Oversample = 6;
                break;
            case Engine.Oversample.X8:
                Oversample = 8;
                break;
            default:
                throw new ArgumentException(string.Format("Invalid oversample value: {0}", options.OverSample.ToString()));
        }

        // Bound the depth between 0 and 1... could arguably use higher depth factors on a low-level depthmap, but more danger of a crash from invalid Zs.
        if (FieldDepth < 0.0)
            FieldDepth = 0.0;

        if (FieldDepth > 1.0)
            FieldDepth = 1.0;

        // Cache some intermediaries
        lineWidth = resolutionX;
        rows = resolutionY;
        _depthWidth = lineWidth;
        _depthScale = Oversample;

        textureWidth = (int)separation;
        textureHeight = (int)((separation * _texture.Height) / _texture.Width);

        // Apply oversampling factor to relevant settings
        if (Oversample > 1)
        {
            separation *= Oversample;
            lineWidth *= Oversample;
            textureWidth *= Oversample;

            if (options.InterpolateDepthMap)
            {
                _depthWidth *= Oversample;
                _depthScale = 1;
            }
        }

        midpoint = lineWidth / 2;

        bytesPerPixel = sizeof(uint);       // Ugh... relies on Pbgra32 pixel format being 32 bits, which obviously it will be, but it's not exactly pretty is it?
        bytesPerRow = lineWidth * bytesPerPixel;
    }

    // For thread safety, can't let the subclasses access these...
    // Pre-allocate massive buffers to hold the data instead :-/
    private readonly DepthMap _depthMap;
    private readonly BitmapType _texture;
    private BitmapSource _bmDepthMap;
    private BitmapSource _bmTexture;
    private WriteableBitmap _wbStereogram;

    private readonly Options _options;

    protected bool RemoveHiddenSurfaces => _options.RemoveHiddenSurfaces;
    protected bool AddConvergenceDots => _options.AddConvergenceDots;
    protected int Oversample { get; set; }

    // Cache the important options as readonly, in the hope it helps with optimisation
    protected readonly int resolutionX;
    protected readonly int resolutionY;
    protected readonly double FieldDepth;
    protected readonly double separation;

    // Worker variables for generation algorithms...
    protected readonly int rows;
    protected readonly int lineWidth;
    protected readonly int midpoint;
    protected readonly int textureWidth;
    protected readonly int textureHeight;
    protected readonly int bytesPerPixel;
    protected readonly int bytesPerRow;

    // Depthwidth = line width if we're interpolating depths... subclasses don't need to know that though
    private readonly int _depthWidth;
    private readonly int _depthScale;

    // Big buffers to hold data... wasteful but necessary if parallelised
    private uint[] _pixels = null;
    private uint[] _texturePixels = null;
    private byte[] _depthBytes = null;

    // Temp hack for progress report & abort
    public int GeneratedLines { get; private set; }
    public int NumLines => rows;
    public bool abort = false;

    public long Milliseconds = 0;

    /// <summary>
    /// Each algorithm operates on a line at a time, so subclasses must implement
    /// the DoLine function with algorithm appropriate functionality
    /// </summary>
    /// <param name="y"></param>
    protected abstract void DoLine(int y);

    /// Let's see how well C# optimises these... would hope they get inlined at least
    protected byte GetDepth(int x, int y)
    {
        return _depthBytes[(y * _depthWidth) + (x / _depthScale)];
    }

    protected float GetDepthFloat(int x, int y)
    {
        return (float)(_depthBytes[(y * _depthWidth) + (x / _depthScale)]) / 255;
    }

    protected uint GetTexturePixel(int x, int y)
    {
        var tp = (((y % textureHeight) * textureWidth) + ((x + midpoint) % textureWidth));
        return _texturePixels[tp];
    }

    protected uint GetStereoPixel(int x, int y)
    {
        var sp = ((y * lineWidth) + x);
        return _pixels[sp];
    }

    protected void SetStereoPixel(int x, int y, uint pixel)
    {
        var sp = ((y * lineWidth) + x);
        _pixels[sp] = pixel;
    }

    // Just for readability
    protected static double SquareOf(double x)
    {
        return x * x;
    }

    // Return which of two values is furthest from a mid-point (or indeed any point)
    protected static int Outermost(int a, int b, int midpoint)
    {
        return (Math.Abs(midpoint - a) > Math.Abs(midpoint - b)) ? a : b;
    }

    /// <summary>
    /// Helper to calculate stereo separation in pixels of a point at depth Z
    /// </summary>
    /// <param name="z"></param>
    /// <returns></returns>
    protected double Sep(double z)
    {
        if (z < 0.0) z = 0.0;
        if (z > 1.0) z = 1.0;
        return ((1 - FieldDepth * z) * (2 * separation) / (2 - FieldDepth * z));
    }

    /// <summary>
    /// Generate the stereogram.  Does all the common functionality, then calls the delegate
    /// set by the subclass to do the actual work.
    /// </summary>
    public BitmapSource Generate()
    {
        // Let's do some profiling
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        // Convert texture to RGB24 and scale it to fit the separation (preserving ratio but doubling width for HQ mode)
        _bmTexture = _texture.GetToScaleAndFormat(textureWidth, textureHeight, PixelFormats.Pbgra32);

        // Resize the depthmap to our target resolution
        _bmDepthMap = _depthMap.GetToScale(_depthWidth, resolutionY);

        // Create a great big 2D array to hold the bytes - wasteful but convenient
        // ... and necessary for parallelisation
        _pixels = new uint[lineWidth * rows];

        // Copy the texture data into a buffer
        _texturePixels = new uint[textureWidth * textureHeight];
        _bmTexture.CopyPixels(new Int32Rect(0, 0, textureWidth, textureHeight), _texturePixels, textureWidth * bytesPerPixel, 0);

        // Copy the depthmap data into a buffer
        _depthBytes = new byte[_depthWidth * rows];
        _bmDepthMap.CopyPixels(new Int32Rect(0, 0, _depthWidth, rows), _depthBytes, _depthWidth, 0);

        // Can mock up a progress indicator
        GeneratedLines = 0;

        // Prime candidate for Parallel.For... yes, about doubles the speed of generation on my Quad-Core
        if (System.Diagnostics.Debugger.IsAttached)   // Don't run parallel when debugging
        {
            for (var y = 0; y < rows; y++)
            {
                DoLine(y);
                if (y > GeneratedLines)
                {
                    GeneratedLines = y;
                }
            }
        }
        else
        {
            Parallel.For(0, rows, y =>
           {
               if (false == abort)
               {
                   DoLine(y);
               }
               if (y > GeneratedLines)
               {
                   GeneratedLines = y;
               }
           });
        }

        if (abort)
        {
            return null;
        }

        // Create a writeable bitmap to dump the stereogram into
        _wbStereogram = new WriteableBitmap(lineWidth, resolutionY, 96.0, 96.0, _bmTexture.Format, _bmTexture.Palette);
        _wbStereogram.WritePixels(new Int32Rect(0, 0, lineWidth, rows), _pixels, lineWidth * bytesPerPixel, 0);

        BitmapSource bmStereogram = _wbStereogram;

        // High quality images need to be scaled back down... 
        if (Oversample > 1)
        {
            double over = Oversample;
            double centre = lineWidth / 2;
            while (over > 1)
            {
                // Scale by steps... could do it in one pass, but quality would depend on what the hardware does?
                var div = Math.Min(over, 2.0);
                //                    double div = over;
                var scale = new ScaleTransform(1.0 / div, 1.0, centre, 0);
                bmStereogram = new TransformedBitmap(bmStereogram, scale);
                over /= div;
                centre /= div;
            }
        }

        if (AddConvergenceDots)
        {
            // Because I made these fields read-only, I can't now restore them... 'spose I could add the dots at hi-res but I'd still need to account for the stretching
            var sep = separation / Oversample;
            double mid = midpoint / Oversample;

            var rtStereogram = new RenderTargetBitmap(bmStereogram.PixelWidth, bmStereogram.PixelHeight, 96.0, 96.0, PixelFormats.Pbgra32);

            var dots = new DrawingVisual();
            var dc = dots.RenderOpen();
            dc.DrawImage(bmStereogram, new Rect(0.0, 0.0, rtStereogram.Width, rtStereogram.Height));
            dc.DrawEllipse(new SolidColorBrush(Colors.Black),
                new Pen(new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)), 1.0),
                new Point(mid - sep / 2, rtStereogram.Height / 16), sep / 16, sep / 16);
            dc.DrawEllipse(new SolidColorBrush(Colors.Black),
                new Pen(new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)), 1.0),
                new Point(mid + sep / 2, rtStereogram.Height / 16), sep / 16, sep / 16);
            dc.Close();

            rtStereogram.Render(dots);

            bmStereogram = rtStereogram;
        }

        // Freeze the bitmap so it can be passed to other threads
        bmStereogram.Freeze();

        timer.Stop();

        Milliseconds = timer.ElapsedMilliseconds;

        return bmStereogram;
    }
}


/// <summary>
/// Generate a stereogram using the Horoptic algorithm.
/// Algorithm Copyright 1996-2012 Simon Booth
/// http://machinewrapped.wordpress.com/stereogrammer/
/// </summary>
internal class StereogramGeneratorHoroptic : StereogramGenerator
{
    protected int[] centreOut;

    private int _hidden = 0;

    public StereogramGeneratorHoroptic(Options options)
        : base(options)
    {
        // Create an array of offsets which alternate pixels from the center out to the edges
        centreOut = new int[lineWidth];
        var offset = midpoint;
        var flip = -1;
        for (var i = 0; i < lineWidth; i++)
        {
            centreOut[i] = offset;
            offset += ((i + 1) * flip);
            flip = -flip;
        }
    }

    protected override void DoLine(int y)
    {
        // Set up a constraints buffer with each pixel initially constrained to equal itself (probably slower than a for loop but easier to step over :p)
        // And convert depths to floats normalised 0..1
        var constraints = new int[lineWidth];
        var depthLine = new float[lineWidth];
        var maxDepth = 0.0f;

        for (var i = 0; i < lineWidth; i++)
        {
            constraints[i] = i;
            depthLine[i] = GetDepthFloat(i, y);
        }

        // Process the line updating any constrained pixels
        for (var ii = 0; ii < lineWidth; ii++)
        {
            // Work from centre out
            var i = centreOut[ii];

            // Calculate Z value of the horopter at this x,y. w.r.t its centre.  20 * separation is approximation of distance to viewer's eyes
            var zh = Math.Sqrt(SquareOf(20 * separation) - SquareOf(i - midpoint));

            // Scale to the range [0,1] and adjust to displacement from the far plane
            zh = 1.0 - (zh / (20 * separation));

            // Separation of pixels on image plane for this point
            // Note - divide ZH by FieldDepth as the horopter is independant
            // of field depth, but sep macro is not.
            var s = (int)Math.Round(Sep(depthLine[i] - (zh / FieldDepth)));

            var left = i - (s / 2);           // The pixel on the image plane for the left eye
            var right = left + s;           // And for the right eye

            if ((0 <= left) && (right < lineWidth))                     // If both points lie within the image bounds ...
            {
                var visible = true;
                if (RemoveHiddenSurfaces)                   // Perform hidden surface test (if requested)
                {
                    var t = 1;
                    double zt = depthLine[i];
                    var delta = 2 * (2 - FieldDepth * depthLine[i]) / (FieldDepth * separation * 2);   // slope of line of sight
                    do
                    {
                        zt += delta;
                        visible = (depthLine[i - t] < zt) && (depthLine[i + t] < zt);           // False if obscured on left or right (can only be obscured by innermost one)
                        t++;
                    }
                    while (visible && zt < maxDepth);  // cache the max depth of the line to minimise checks needed
                }
                if (visible)
                {
                    // Decide whether we want to constrain the left or right pixel
                    // Want to avoid constraint loops, so always constrain outermost pixel to innermost
                    // Should depend if one or the other is already constrained I suppose
                    var constrainee = Outermost(left, right, midpoint);
                    var constrainer = (constrainee == left) ? right : left;

                    // Find an unconstrained pixel and constrain ourselves to it
                    // Uh-oh, what happens if they become constrained to each other?  Constrainee is flagged as unconstrained, I suppose
                    while (constraints[constrainer] != constrainer)
                        constrainer = constraints[constrainer];

                    constraints[constrainee] = constrainer;
                }
                else
                {
                    _hidden++;
                }

                // Points can only be hidden by a point closer to the centre, i.e. one we've already processed
                if (depthLine[i] > maxDepth)
                    maxDepth = depthLine[i];
            }
        }

        // Now actually set the pixels
        for (var i = 0; i < lineWidth; i++)
        {
            var pix = i;

            // Find an unconstrained pixel
            while (constraints[pix] != pix)
                pix = constraints[pix];

            // And get the RGBs from the tiled texture at that point
            SetStereoPixel(i, y, GetTexturePixel(pix, y));
        }
    }
}


/// <summary>
/// Generate a stereogram using the Constraint Satisfaction algorithm
/// Copyright 1993 I. H. Witten, S. Inglis and H. W. Thimbleby
/// http://www.cs.waikato.ac.nz/pubs/wp/1993/#9302
/// </summary>
internal class StereogramGeneratorConstraintSatisfaction : StereogramGenerator
{
    public StereogramGeneratorConstraintSatisfaction(Options options)
        : base(options)
    {
    }

    /// <summary>
    /// Process a line of the image using the constraint satisfaction algorithm
    /// </summary>
    protected override void DoLine(int y)
    {
        throw new NotImplementedException("Removed from Open Source version");
    }
}

/// <summary>
/// Generate a stereogram using the Lookback algorithm
/// Copyright 1979 Christopher Tyler & Maureen Clarke
/// </summary>
internal class StereogramGeneratorLookBack : StereogramGenerator
{
    public StereogramGeneratorLookBack(Options options)
        : base(options)
    {
    }

    protected override void DoLine(int y)
    {
        throw new NotImplementedException("Removed from Open Source version");
    }
}

/// <summary>
/// Generate a stereogram using the Tyler-Chang algorithm
/// Copyright unknown, circa 1977 Christopher Tyler & J.J. Chang
/// </summary>
internal class StereogramGeneratorTylerChang : StereogramGenerator
{
    public StereogramGeneratorTylerChang(Options options)
        : base(options)
    {
    }

    protected override void DoLine(int y)
    {
        throw new NotImplementedException("Removed from Open Source version");
    }
}

/// <summary>
/// Techmind algorith for stereogram generation, 
/// Copyright 1995-2001 Andrew Steer.
/// http://www.techmind.org/stereo/stech.html
/// </summary>
internal class StereogramGeneratorTechmind : StereogramGenerator
{
    public StereogramGeneratorTechmind(Options options)
        : base(options)
    {
    }

    protected override void DoLine(int y)
    {
        throw new NotImplementedException("Removed from Open Source version");
    }
}

