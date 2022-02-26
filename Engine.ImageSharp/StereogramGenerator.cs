// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/
//
// See individual classes for algorithm copyrights

namespace Engine;

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
        return new StereogramGeneratorHoroptic(options);
    }

    /// <summary>
    /// Constructor caches the important options and does some common setup work
    /// </summary>
    public StereogramGenerator(Options options)
    {
        _options = options;

        resolutionX = options.ResolutionX;
        resolutionY = options.ResolutionY;
        separation = options.Separation;
        FieldDepth = options.FieldDepth;

        // Bound the depth between 0 and 1... could arguably use higher depth factors on a low-level depthmap, but more danger of a crash from invalid Zs.
        if (FieldDepth < 0.0)
            FieldDepth = 0.0;

        if (FieldDepth > 1.0)
            FieldDepth = 1.0;

        // Cache some intermediaries
        lineWidth = resolutionX;
        rows = resolutionY;
        _depthWidth = lineWidth;
        _depthScale = 1;

        midpoint = lineWidth / 2;

        bytesPerPixel = sizeof(uint);       // Ugh... relies on Pbgra32 pixel format being 32 bits, which obviously it will be, but it's not exactly pretty is it?
        bytesPerRow = lineWidth * bytesPerPixel;
    }

    private readonly Options _options;

    protected bool RemoveHiddenSurfaces => _options.RemoveHiddenSurfaces;

    // Cache the important options as readonly, in the hope it helps with optimisation
    protected readonly int resolutionX;
    protected readonly int resolutionY;
    protected readonly double FieldDepth;
    protected readonly double separation;

    // Worker variables for generation algorithms...
    protected readonly int rows;
    protected readonly int lineWidth;
    protected readonly int midpoint;
    protected readonly int bytesPerPixel;
    protected readonly int bytesPerRow;

    // Depthwidth = line width if we're interpolating depths... subclasses don't need to know that though
    private readonly int _depthWidth;
    private readonly int _depthScale;

    // Big buffers to hold data... wasteful but necessary if parallelised
    private Image<Rgb24> _pixels = null!;
    private Image<Rgb24> _texturePixels = null!;
    private Image<A8> _depthBytes = null!;

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
        return _depthBytes[x / _depthScale, y].PackedValue;
    }

    protected float GetDepthFloat(int x, int y)
    {
        return GetDepth(x, y) / 255f;
    }

    protected Rgb24 GetTexturePixel(int x, int y)
    {
        return _texturePixels[(x + midpoint) % _texturePixels.Width, y % _texturePixels.Height];
    }

    protected void SetStereoPixel(int x, int y, Rgb24 pixel)
    {
        _pixels[x, y] = pixel;
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
    public Image Generate(Image depthMap, Image texture)
    {
        var textureWidth = (int)separation;
        var textureHeight = (int)((separation * texture.Height) / texture.Width);

        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        _pixels = new Image<Rgb24>(lineWidth, rows);

        _texturePixels = texture
            .CloneAs<Rgb24>()
            .Clone(op => op.Resize(textureWidth, textureHeight));

        _depthBytes = depthMap
            .CloneAs<A8>()
            .Clone(op => op.Resize(_depthWidth, resolutionY));

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

        return _pixels;
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
