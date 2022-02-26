// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/

namespace Engine;

/// <summary>
/// Wrap up the options for stereogram generation for easy sharing and serialization
/// </summary>
public class Options
{
    public int ResolutionX { get; set; } = 1024;
    public int ResolutionY { get; set; } = 768;
    public double Separation { get; set; } = 128.0;
    public double FieldDepth { get; set; } = 0.3333;

    public bool RemoveHiddenSurfaces { get; set; } = true;
}