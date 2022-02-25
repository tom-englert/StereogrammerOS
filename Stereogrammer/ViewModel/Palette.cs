﻿// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Engine;
using Stereogrammer.Model;

namespace Stereogrammer.ViewModel
{
    public class Palette
    {
        /// <summary>
        /// Each palette can have a default action associated with double clicking a thumbnail in it
        /// </summary>
        public RoutedCommand DefaultDoubleClick { get; set; }

        /// <summary>
        /// Callback when a thumbnail is selected, to allow UI updates
        /// </summary>
        /// <param name="palette"></param>
        /// <param name="thumb"></param>
        public delegate void ThumbnailSelected( Palette palette, Thumbnail thumb );

        public event ThumbnailSelected OnThumbnailSelected;

        private Thumbnail _mySelectedThumbnail = null;

        /// <summary>
        /// Make thumbnails an observable collection, for data-binding purposes
        /// </summary>
        private readonly ObservableCollection<Thumbnail> _myThumbnails = new ObservableCollection<Thumbnail>();

        public ObservableCollection<Thumbnail> Thumbnails => _myThumbnails;

        private bool _bHasMultiselection = false;

        public int NumUnremovable = 0;

        public string SDefaultDirectory { get; set; }

        /// <summary>
        /// Underlying bitmap collection
        /// </summary>
        private BitmapCollection _bitmaps;
        public BitmapCollection Bitmaps
        {
            get => _bitmaps;
            set
            {
                _bitmaps = value;
                _bitmaps.OnItemAdded += palette_itemAdded;
                _bitmaps.OnItemRemoved += palette_itemRemoved;
            }
        }

        public Palette( BitmapCollection collection )
            : base()
        {
            Bitmaps = collection;
        }

        public BitmapSource SelectedImage => ( _mySelectedThumbnail != null ) ? _mySelectedThumbnail.ThumbnailOf.Bitmap : null;

        public int NumThumbnails => _myThumbnails.Count;

        /// <summary>
        /// Callback event to add thumbnail when the underlying collection adds an item
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="item"></param>
        private void palette_itemAdded( BitmapCollection collection, BitmapType item )
        {
            AddThumbnail( item );
        }

        /// <summary>
        /// Callback event to remove the thumbnail when the underlying collection removes an item
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="item"></param>
        private void palette_itemRemoved( BitmapCollection collection, BitmapType item )
        {
            var thumb = FindThumbnail( item );
            RemoveThumbnail( thumb );
        }

        /// <summary>
        /// Add a thumbnailable item to the palette
        /// </summary>
        /// <returns></returns>
        public Thumbnail AddThumbnail( BitmapType item, RoutedCommand onDoubleClick = null )
        {
            if ( onDoubleClick == null )
            {
                onDoubleClick = DefaultDoubleClick;
            }

            var thumb = new Thumbnail( item );
            thumb.OnDoubleClick = onDoubleClick;
            
            _myThumbnails.Add( thumb );

            if ( thumb.CanRemove == false )
                NumUnremovable++;

            SelectItem( thumb );
            return thumb;
        }

        /// <summary>
        /// Remove an item from the palette
        /// </summary>
        /// <param name="thumb"></param>
        public void RemoveThumbnail( Thumbnail thumb )
        {
            var index = _myThumbnails.IndexOf( thumb );

            _myThumbnails.Remove( thumb );

            if ( thumb.CanRemove == false )
                NumUnremovable--;

            if ( _mySelectedThumbnail == thumb )
	        {
                var iNumThumbs = _myThumbnails.Count;
                if ( iNumThumbs > 0 )
                {
                    SelectItem( _myThumbnails[ Math.Min( index, iNumThumbs - 1 ) ] );
                }
                else
                {
                    _mySelectedThumbnail = null;
                }
	        }
        }

        /// <summary>
        /// Clear the palette... remove all removable thumbnails
        /// </summary>
        public void Clear()
        {
            // Just build a new list and swap it out
            Bitmaps.Clear();

            if ( _myThumbnails.Count > 0 )
            {
                SelectItem( _myThumbnails[ 0 ] );
            }
            else
            {
                _mySelectedThumbnail = null;
            }
        }

        /// <summary>
        /// There can only be one selected thumbnail at a time
        /// </summary>
        /// <returns></returns>
        public Thumbnail GetSelectedThumbnail()
        {
            return _mySelectedThumbnail;
        }

        /// <summary>
        /// Get multiselected thumbnails
        /// </summary>
        /// <returns></returns>
        public List<Thumbnail> GetMultiselectedThumbnails()
        {
            return _myThumbnails.ToList().FindAll( x => x.MultiSelected );
        }

        /// <summary>
        /// There can only be one selected item at a time
        /// </summary>
        /// <returns></returns>
        public BitmapType GetSelectedItem()
        {
            return _mySelectedThumbnail == null ? null : _mySelectedThumbnail.ThumbnailOf;
        }

        /// <summary>
        /// Get multiselected items
        /// </summary>
        /// <returns></returns>
        public List<BitmapType> GetMultiselectedItems()
        {
            return (from thumb in Thumbnails
                    where thumb.MultiSelected
                    select thumb.ThumbnailOf).ToList();
        }

        /// <summary>
        /// Set the one and only selected thumbnail
        /// </summary>
        /// <param name="item"></param>
        public Thumbnail SelectItem( Thumbnail thumb )
        {
            // We won't allow a NULL selection, 'cause we don't want null references being thrown around
            if ( thumb != null )
            {
                if ( _mySelectedThumbnail != null )
                {
                    _mySelectedThumbnail.Selected = false;
                }

                _mySelectedThumbnail = thumb;

                thumb.Selected = true;

                // Want the image to bind dynamically to something really, but... data binding is confusing
                if ( OnThumbnailSelected != null )
                {
                    OnThumbnailSelected( this, thumb );
                }
            }

            return thumb;
        }

        public Thumbnail SelectItem( BitmapType item )
        {
            return SelectItem( FindThumbnail( item ) );
        }

        /// <summary>
        /// Keyboard event handler for palette... need to assign it to each thumbnail too, I guess?
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void RemoveSelectedItems()
        {
            if ( _mySelectedThumbnail != null && _mySelectedThumbnail.CanRemove )
            {
                if ( _mySelectedThumbnail.CanRemove )
                {
                    Bitmaps.RemoveItem( _mySelectedThumbnail.ThumbnailOf );
                }
            }
            if ( _bHasMultiselection )
            {
                foreach ( var thumb in GetMultiselectedThumbnails() )
                {
                    if ( thumb.CanRemove )
                    {
                        Bitmaps.RemoveItem( thumb.ThumbnailOf );
                    }
                }
            }
        }

        /// <summary>
        /// Find the thumnail that matches the specified item
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public Thumbnail FindThumbnail( BitmapType item )
        {
            foreach ( var thumb in _myThumbnails )
            {
                if ( thumb.ThumbnailOf == item )
                    return thumb;
            }

            return null;
        }

        /// <summary>
        /// Find the thumnail that matches the specified item
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public Thumbnail FindThumbnail( string name )
        {
            foreach ( var thumb in _myThumbnails )
            {
                if ( thumb.Name == name )
                    return thumb;
            }

            return null;
        }

        public Thumbnail FindByFilename( string filename )
        {
            foreach ( var thumb in _myThumbnails )
            {
                if ( thumb.FileName == filename )
                    return thumb;
            }

            return null;
        }

        /// <summary>
        /// Accessors for lists for data-binding purposes
        /// </summary>
        /// <returns></returns>
        public List<BitmapType> GetItems()
        {
            return Bitmaps.GetItems();
        }

        public List<string> GetItemNames()
        {
            return Bitmaps.GetItemNames();
        }

        public List<string> GetFilenames()
        {
            return Bitmaps.GetFilenames();
        }

        public void SelectionLogic( Thumbnail thumb, bool shift, bool ctrl )
        {
            Console.WriteLine( string.Format( "Item: {0} Shift: {1} CTRL: {2}", thumb, shift, ctrl ) );

            // Selection & multi-selection logic... surprisingly complicated, isn't it?  Think this is idiomatic usage of CTRL+SHIFT modifiers now.
            // WPF would probably do all of this for me naturally if I figured out the right way to make Palettes a custom/user control...
            if ( shift )
            {
                // if shift key is down, we're range-selecting (deselecting?)
                var index1 = _myThumbnails.IndexOf( thumb );
                var index2 = ( _mySelectedThumbnail == null ) ? 0 : _myThumbnails.IndexOf( _mySelectedThumbnail );
                if ( index1 > index2 )
                {
                    var t = index1;
                    index1 = index2;
                    index2 = t;
                }
                if ( Keyboard.IsKeyDown( Key.LeftCtrl ) || Keyboard.IsKeyDown( Key.RightCtrl ) )
                {
                    // CTRL+SHIFT = additive range selection
                    for ( var i = index1; i <= index2; i++ )
                    {
                        _myThumbnails[ i ].MultiSelected = true;
                    }
                    SelectItem( thumb );
                }
                else
                {
                    // SHIFT alone = exclusive range selection
                    for ( var i = 0; i < _myThumbnails.Count; i++ )
                    {
                        _myThumbnails[ i ].MultiSelected = ( i >= index1 && i <= index2 );
                    }

                    if ( index1 != index2 )
                    {
                        _bHasMultiselection = true;
                    }
                    else
                    {
                        SelectItem( thumb );
                        _bHasMultiselection = false;
                    }
                }
            }
            else if ( ctrl )
            {
                // If CTRL is held down, we're multi-selecting
                if ( thumb.MultiSelected == false )
                {
                    if ( _mySelectedThumbnail != null )
                    {
                        _mySelectedThumbnail.MultiSelected = true;
                    }
                    thumb.MultiSelected = true;
                    _bHasMultiselection = true;
                    SelectItem( thumb );
                }
                else
                {
                    thumb.MultiSelected = false;
                }
            }
            else
            {
                foreach ( var t in _myThumbnails )
                {
                    if ( t.MultiSelected )
                        t.MultiSelected = false;
                }
                _bHasMultiselection = false;
                SelectItem( thumb );
            }

        }

    }
}
