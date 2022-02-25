// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Engine;
using Stereogrammer.Model;


namespace Stereogrammer.ViewModel
{
    /// <summary>
    /// Helper for thumbnails in the palette
    /// </summary>
    public class Thumbnail : DependencyObject
    {
        public static readonly DependencyProperty BorderBrushProperty =
              DependencyProperty.Register("BorderBrush", typeof(Brush), typeof(Thumbnail));

        public BitmapType ThumbnailOf { get; private set; }

        public bool CanRemove
        {
            get => ThumbnailOf.CanRemove;
            set => ThumbnailOf.CanRemove = value;
        }

        public RoutedCommand OnDoubleClick { get; set; }

        /// <summary>
        /// Thumbnail has exclusive selection in this palette
        /// </summary>
        public bool Selected
        {
            get => _bSelected;
            set
            {
                _bSelected = value;
                SetBorderBrush();
                Source = ThumbnailOf.GetThumbnail(_bSelected);
            }
        }

        /// <summary>
        /// Thumbnail is one of the selected thumbnails in this palette
        /// </summary>
        public bool MultiSelected
        {
            get => _bMultiselected;
            set
            {
                _bMultiselected = value;
                SetBorderBrush();
            }
        }

        public string Name => ThumbnailOf.Name;

        public string FileName => ThumbnailOf.FileName;

        public string Description => string.Format("{0} ({1}x{2})", Name, ThumbnailOf.PixelWidth, ThumbnailOf.PixelHeight);

        public override string ToString()
        {
            return Description;
        }

        public ImageSource Source { get; private set; }

        public Brush BorderBrush
        {
            get => (Brush)GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        private bool _bSelected = false;
        private bool _bMultiselected = false;

        public Thumbnail(BitmapType represents)
            : base()
        {
            ThumbnailOf = represents;

            Source = ThumbnailOf.GetThumbnail(false);

            //            ContextMenu = GetContextMenu();
        }

        /// <summary>
        /// Border colour based on selected status
        /// </summary>
        private void SetBorderBrush()
        {
            if (_bSelected)
                BorderBrush = new SolidColorBrush(Colors.Green);
            else if (_bMultiselected)
                BorderBrush = new SolidColorBrush(Colors.Blue);
            else
                BorderBrush = new SolidColorBrush(Colors.White);
        }

        private List<CommandView> _commands;
        public List<CommandView> SupportedCommands
        {
            get
            {
                if (_commands == null)
                {
                    _commands = Commands.GetSupportedCommands(ThumbnailOf);
                    _commands.Add(Commands.CmdDeleteSelectedItems);
                }
                return _commands;
            }
        }

    }



}
