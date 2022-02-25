// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/

using System;

namespace Engine;

/// <summary>
/// Wrap up the options for stereogram generation for easy sharing and serialization
/// </summary>
public class Options
{
    public int ResolutionX { get; set; }
    public int ResolutionY { get; set; }
    public double Separation { get; set; }
    public double FieldDepth { get; set; }

    public bool RemoveHiddenSurfaces { get; set; }
    public bool AddConvergenceDots { get; set; }
    public bool PreserveAspectRatio { get; set; }
    public bool InterpolateDepthMap { get; set; }

    public Algorithm Algorithm { get; set; }
    public Oversample OverSample { get; set; }

    public DepthMap DepthMap { get; set; }
    public Texture Texture { get; set; }

    public DateTime time = DateTime.Now;

    public Options()
    {
        ResolutionX = 1024;
        ResolutionY = 768;
        Separation = 128.0;
        FieldDepth = 0.3333;
        RemoveHiddenSurfaces = false;
        AddConvergenceDots = false;
        PreserveAspectRatio = true;
        InterpolateDepthMap = true;
        OverSample = Oversample.X2;
    }

    public Options( Options options )
    {
        ResolutionX = options.ResolutionX;
        ResolutionY = options.ResolutionY;
        Separation = options.Separation;
        FieldDepth = options.FieldDepth;
        OverSample = options.OverSample;
        RemoveHiddenSurfaces = options.RemoveHiddenSurfaces;
        AddConvergenceDots = options.AddConvergenceDots;
        PreserveAspectRatio = options.PreserveAspectRatio;
        InterpolateDepthMap = options.InterpolateDepthMap;
        Algorithm = options.Algorithm;
        DepthMap = options.DepthMap;
        Texture = options.Texture;
    }

}