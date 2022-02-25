// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/

using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System.IO;
using Engine;

namespace Stereogrammer.Model;

/// <summary>
/// A collection of Bitmap Types.
/// Doesn't actually add much to the model layer, and could (maybe should) be rolled into Palette at the ViewModel layer for simplification.
/// </summary>
public class BitmapCollection
{
    private Func<BitmapImage, BitmapType> FactoryFunc { get; set; }

    private readonly List<BitmapType> _myItems = new List<BitmapType>();

    public delegate void ItemCallback( BitmapCollection collection, BitmapType item );

    public event ItemCallback OnItemAdded;
    public event ItemCallback OnItemRemoved;

    public BitmapCollection( Func<BitmapImage, BitmapType> factoryFunc )
    {
        FactoryFunc = factoryFunc;
    }

    /// <summary>
    /// Add a BitmapType object to the collection
    /// </summary>
    /// <param name="item"></param>
    public void AddItem( BitmapType item, bool bCanRemove = true )
    {
        item.CanRemove = bCanRemove;
        _myItems.Add( item );
        if ( null != OnItemAdded )
        {
            OnItemAdded( this, item );
        }
    }

    /// <summary>
    /// Create a BitmapType object from a BitmapImage using the factory function, and add it to the collection
    /// </summary>
    /// <param name="item"></param>
    public BitmapType AddNewItem( BitmapImage bitmap )
    {
        var item = FactoryFunc( bitmap );
        AddItem( item );
        return item;
    }

    /// <summary>
    /// Remove an item from the collection
    /// </summary>
    /// <param name="item"></param>
    public void RemoveItem( BitmapType item )
    {
        if ( _myItems.Remove( item ) )
        {
            if ( null != OnItemRemoved )
            {
                OnItemRemoved( this, item );                    
            }
        }            
    }

    /// <summary>
    /// Accessors for lists for data-binding purposes
    /// </summary>
    /// <returns></returns>
    public List<BitmapType> GetItems()
    {
        var items = new List<BitmapType>();
        foreach ( var item in _myItems )
            items.Add( item );
        return items;
    }

    public List<string> GetItemNames()
    {
        var names = new List<string>();
        foreach ( var item in _myItems )
            names.Add( item.Name );
        return names;
    }

    public List<string> GetFilenames()
    {
        var filenames = new List<string>();
        foreach ( var item in _myItems )
        {
            if ( item.FileName != null )
            {
                filenames.Add( item.FileName );
            }
        }
        return filenames;
    }

    /// <summary>
    /// Create a BitmapType from a file.  Must have set the factory function to use.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="uri"></param>
    /// <returns></returns>
    public BitmapType CreateItemFromFile( string name, Uri uri )
    {
        var image = new BitmapImage( uri );
        var x = AddNewItem( image );
        x.Name = name;
        if ( uri.IsFile )
        {
            x.FileName = Uri.UnescapeDataString( uri.AbsolutePath );
        }
        else
        {
            x.FileName = uri.AbsoluteUri;
        }
        return x;
    }

    public BitmapType CreateItemFromResource( string name, string path )
    {
        var uri = new Uri( new Uri( @"pack://application:,,,/MyAssembly;component" ), path );
        var image = new BitmapImage( uri );
        var x = AddNewItem( image );
        x.Name = name;
        x.FileName = uri.AbsoluteUri;
        return x;
    }

    /// <summary>
    /// Populate the palette from a list of filenames
    /// </summary>
    /// <param name="filenames"></param>
    public void Populate( string[] filenames )
    {
        foreach ( var filename in filenames )
        {
            try
            {
                var uri = new Uri( filename );
                if ( uri.IsFile )
                {
                    var file = new FileInfo( filename );
                    var name = Path.GetFileNameWithoutExtension( file.Name );
                    var item = CreateItemFromFile( name, uri );
                }
                else
                {
                    var name = Path.GetFileNameWithoutExtension( uri.LocalPath );
                    var item = CreateItemFromFile( name, uri );
                }
            }
            catch ( Exception )
            {
                Console.WriteLine( "Failed to load {0}", filename );
            }
        }
    }

    public void Populate( System.Collections.Specialized.StringCollection filenames )
    {
        var strings = new string[ filenames.Count ];
        filenames.CopyTo( strings, 0 );
        Populate( strings );
    }

    /// <summary>
    /// Clear the collection, except for any items marked as unremovable
    /// </summary>
    public void Clear()
    {
        var items = _myItems.ToArray();
        foreach ( var x in items )
        {
            if ( x.CanRemove )
            {
                RemoveItem( x );
            }
        }
    }
    
}

/// <summary>
/// Specialised BitmapCollection (basically provides a custom factory func)
/// </summary>
public class StereogramCollection : BitmapCollection
{
    public StereogramCollection()
        : base(new Func<BitmapSource, BitmapType>(bmp => new Stereogram(bmp)))
    {
    }
}
