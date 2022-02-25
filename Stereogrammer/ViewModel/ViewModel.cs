using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using Engine;
using Microsoft.Win32;

using Stereogrammer.Model;

namespace Stereogrammer.ViewModel
{
    public class TextureCollection : BitmapCollection
    {
        public TextureCollection()
            : base(new Func<BitmapSource, BitmapType>(bmp => new Texture(bmp)))
        {
        }
    }
    public class DepthmapCollection : BitmapCollection
    {
        public DepthmapCollection()
            : base(new Func<BitmapSource, BitmapType>(bmp => new DepthMap(bmp)))
        {
        }
    }

    internal class TextureGreyDots : Texture
    {
        public TextureGreyDots(int resX, int resY)
            : base(TextureType.Greydots, resX, resY)
        {
        }

        public override BitmapSource GetThumbnail(bool bSelected)
        {
            if (bSelected)
            {
                Bitmap = GenerateRandomDots(Bitmap.PixelWidth, Bitmap.PixelHeight);
            }

            return Bitmap;
        }
    }

    internal class TextureColourDots : Texture
    {
        public TextureColourDots(int resX, int resY)
            : base(TextureType.Colourdots, resX, resY)
        {
        }

        public override BitmapSource GetThumbnail(bool bSelected)
        {
            if (bSelected)
            {
                Bitmap = GenerateColoredDots(Bitmap.PixelWidth, Bitmap.PixelHeight);
            }

            return Bitmap;
        }
    }

    internal class StereogrammerViewModel
    {
        public Options Options { get; set; }

        public DepthmapCollection myDepthmaps = new DepthmapCollection();
        public TextureCollection myTextures = new TextureCollection();
        public StereogramCollection myStereograms = new StereogramCollection();

        private DepthMap _depthMapFlat = null;
        private Texture _textureRandomDots = null;
        private Texture _textureRandomColours = null;

        public Palette DepthmapPalette { get; set; }
        public Palette TexturePalette { get; set; }
        public Palette StereogramPalette { get; set; }

        public ProgressMonitor monitor;

        // Keep an observable collection of palettes... could databind tab control to the collection,
        // But lose a fair amount of flexibility in the XAML by doing so
        public ObservableCollection<Palette> Palettes { get; private set; }

        /// <summary>
        /// Callback for when a palette is selected
        /// </summary>
        public event Action<Palette> OnPaletteSelected;

        /// <summary>
        /// Callback for when a new object is set for previewing
        /// </summary>
        public event Action<object> OnPreviewItemChanged;

        /// <summary>
        /// Callback for displaying an error message
        /// </summary>
        public event Action<string> OnErrorMessage;

        /// <summary>
        /// Item set for previewing
        /// </summary>
        private object _previewItem = null;
        public object PreviewItem { 
            get => _previewItem;
            set
            {
                if ( _previewItem != value )
                {
                    _previewItem = value;
                    if ( OnPreviewItemChanged != null )
                    {
                        OnPreviewItemChanged( _previewItem );
                    }                
                }
            }
        }

        // Would like to databind selected palette to something in the view, but a callback is more flexible
        private Palette _selectedPalette = null;
        public Palette SelectedPalette
        {
            get => _selectedPalette;
            set { 
                if ( _selectedPalette != value )
                {
                    _selectedPalette = value;
                    if ( OnPaletteSelected != null )
                    {
                        OnPaletteSelected( _selectedPalette );
                    }                
                }
            }
        }

        /// <summary>
        /// Get the item which has been selected in the Texture Palette
        /// </summary> 
        public Texture SelectedTexture
        {
            get
            {
                if ( TexturePalette != null && TexturePalette.GetSelectedThumbnail() != null )
                {
                    var thumb = TexturePalette.GetSelectedThumbnail();
                    return (Texture)thumb.ThumbnailOf;
                }
                return null;
            }
        }

        /// <summary>
        /// Get the item which has been selected in the Depthmap Palette
        /// </summary>
        public DepthMap SelectedDepthMap
        {
            get
            {
                if ( DepthmapPalette != null && DepthmapPalette.GetSelectedThumbnail() != null )
                {
                    var thumb = DepthmapPalette.GetSelectedThumbnail();
                    return (DepthMap)thumb.ThumbnailOf;
                }
                return null;
            }
        }

        /// <summary>
        /// Get the item which has been selected in the Stereogram Palette
        /// </summary>
        public Stereogram SelectedStereogram
        {
            get
            {
                if ( StereogramPalette != null && StereogramPalette.GetSelectedThumbnail() != null )
                {
                    var thumb = StereogramPalette.GetSelectedThumbnail();
                    return (Stereogram)thumb.ThumbnailOf;
                }
                return null;
            }
        }

        // Logical solution to this still elusive
        private readonly StereogramGeneratorAsync _previewer = null;
        private Stereogram _previewStereogram = null;
        public Stereogram PreviewStereogram
        {
            get => _previewStereogram;
            private set
            {
                _previewStereogram = value;
                PreviewItem = _previewStereogram;
            }
        }

        /// <summary>
        /// Callback type for generated stereograms
        /// </summary>
        /// <param name="stereogram"></param>
        public delegate void StereogramGenerated( Stereogram stereogram );

        /// <summary>
        /// Constructor for the view model - binds to the model
        /// </summary>
        /// <param name="model"></param>
        public StereogrammerViewModel()
        {
            Options = new Options();

            DepthmapPalette = AddPalette( myDepthmaps, Commands.CmdPreviewStereogram );
            TexturePalette = AddPalette( myTextures, Commands.CmdPreviewStereogram );
            StereogramPalette = AddPalette( myStereograms, Commands.CmdPreviewStereogram );

            _previewer = new StereogramGeneratorAsync( new Action<Stereogram>( stereogram => 
                    {
                        if ( stereogram != null )
                            PreviewStereogram = stereogram;
                        else
                            ErrorMessage( "Preview failed!" );
                        EndMonitoring(); 
                    } ) );
        }

        /// <summary>
        /// Register a palette
        /// </summary>
        /// <param name="p"></param>
        /// <param name="doubleClick"></param>
        private Palette AddPalette( BitmapCollection collection, RoutedCommand doubleClick = null )
        {
            var p = new Palette( collection );

            if ( doubleClick != null )
            {
                p.DefaultDoubleClick = doubleClick;                
            }
            p.OnThumbnailSelected += event_ThumbnailSelected;

            if ( Palettes == null )
            {
                Palettes = new ObservableCollection<Palette>();
            }
            Palettes.Add( p );
            return p;
        }


        /// <summary>
        /// Accessors to deeper levels
        /// </summary>
        /// <returns></returns>
        public List<BitmapType> GetDepthmaps()
        {
            return DepthmapPalette.GetItems();
        }

        public List<BitmapType> GetTextures()
        {
            return TexturePalette.GetItems();
        }

        /// <summary>
        /// Helper to generate a stereogram
        /// </summary>
        /// <param name="options"></param>
        /// <param name="bSave"></param>
        /// <param name="bAddThumbnail"></param>
        /// <returns></returns>
        public void GenerateStereogram( Options options, StereogramGenerated callback = null )
        {
            var generator = new StereogramGeneratorAsync( stereogram => OnStereogramGenerated( stereogram, callback ) );
            generator.RequestStereogram( options );
            MonitorProgress( () => (float)generator.GetProgress() );
        }

        private void OnStereogramGenerated( Stereogram stereogram, StereogramGenerated callback )
        {
            if ( stereogram != null )
            {
                myStereograms.AddItem( stereogram );
                SelectedPalette = StereogramPalette;
            }

            if ( callback != null )
            {
                callback( stereogram );
            }

            EndMonitoring();
        }

        /// <summary>
        /// Helper to generate a preview stereogram - at the moment there is only one generator, so
        /// requests for multiple previews will pre-empt each other.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="bSave"></param>
        /// <param name="bAddThumbnail"></param>
        /// <returns></returns>
        public StereogramGeneratorAsync RequestPreview( Options options, long millisecondDelay = 0 )
        {
            _previewer.RequestStereogram( options, millisecondDelay );
            MonitorProgress( () => (float)_previewer.GetProgress() );
            return _previewer;
        }

        /// <summary>
        /// Update the preview pane... i.e. if the preview pane is showing the preview stereogram, regenerate it
        /// </summary>
        /// <param name="millisecondDelay"></param>
        public void UpdatePreview( int previewWidth, int previewHeight, long millisecondDelay = 0 )
        {
            if ( PreviewItem != null && PreviewItem == PreviewStereogram && PreviewStereogram.HasOptions )
            {
                Options previewOptions = new Options( Options );
                previewOptions.DepthMap = PreviewStereogram.Options.DepthMap;
                previewOptions.Texture = PreviewStereogram.Options.Texture;
                previewOptions.ResolutionX = previewWidth;
                previewOptions.ResolutionY = previewHeight;
                RequestPreview( previewOptions, millisecondDelay );
            }
        }

        /// <summary>
        /// Register a progress reporter, e.g. a status bar
        /// </summary>
        /// <param name="report"></param>
        /// <param name="complete"></param>
        /// <returns></returns>
        public ProgressMonitor RegisterProgressReporter( ProgressMonitor.ReportStatus report, ProgressMonitor.OnStart start = null, ProgressMonitor.OnCompletion complete = null )
        {
            if ( monitor != null )
            {
                monitor.EndMonitoring();
                monitor.Dispose();
                monitor = null;
            }

            if ( report != null )
            {
                monitor = new ProgressMonitor( report, start, complete );
            }

            return monitor;
        }

        /// <summary>
        /// Start reporting progress of an operation
        /// </summary>
        /// <param name="progress"></param>
        public void MonitorProgress( ProgressMonitor.UpdateProgress progress )
        {
            if ( monitor != null )
            {
                monitor.MonitorProgress( progress );
            }
        }

        public void EndMonitoring()
        {
            if ( monitor != null )
            {
                monitor.EndMonitoring();
            }
        }



        /// <summary>
        /// Save a stereogram
        /// </summary>
        /// <param name="stereogram"></param>
        public void SaveStereogram( Stereogram stereogram )
        {
            Debug.Assert( stereogram != null );
            SaveBitmapToFile( stereogram, "Save Stereogram", StereogramPalette.SDefaultDirectory );
        }

        /// <summary>
        /// Restore settings from a stereogram
        /// </summary>
        /// <param name="stereogram"></param>
        public void RestoreStereogramSettings( Stereogram stereogram )
        {
            if ( stereogram != null && stereogram.Options != null )
            {
                DepthmapPalette.SelectItem( stereogram.Options.DepthMap );
                TexturePalette.SelectItem( stereogram.Options.Texture );
                Options = new Options( stereogram.Options );
                PreviewItem = stereogram;
            }
        }

        /// <summary>
        /// Save the thumbnailable item to an image file, using a SaveFileDialog and
        /// deducing the file type from the specified file extension.  Mixing business logic
        /// and presentation up even more, but it does seem to be the 'natural' place for the function.
        /// </summary>
        /// <param name="dialogTitle"></param>
        /// <param name="initialDirectory"></param>
        public void SaveBitmapToFile( BitmapType bitmap, string dialogTitle, string initialDirectory )
        {
            var saveDialog = new SaveFileDialog();
            saveDialog.AddExtension = true;
            saveDialog.InitialDirectory = initialDirectory;
            saveDialog.FileName = bitmap.Name;
            saveDialog.OverwritePrompt = true;
            saveDialog.Title = dialogTitle;
            saveDialog.ValidateNames = true;
            saveDialog.Filter = "JPG file (*.jpg)|*.jpg|BMP file (*.bmp)|*.bmp|PNG file (*.png)|*.png";

            var result = saveDialog.ShowDialog();

            if ( result != true )
                return;

            FileType[] types = { FileType.Jpg, FileType.Bmp, FileType.Png };

            var type = Math.Max( 0, saveDialog.FilterIndex - 1 );

            if ( type >= types.Length )
            {
                throw new ArgumentException( "Invalid file type" );
            }

            var file = new FileInfo( saveDialog.FileName );
            var directory = file.Directory;

            if ( directory.Exists == false )
            {
                throw new ArgumentException( "Invalid directory" );
            }

            bitmap.SaveImage( file.FullName, types[ type ] );
        }

        /// <summary>
        /// Return a new depthmap which is the depth-inversion of the presented depthmap
        /// </summary>
        /// <param name="dm"></param>
        /// <returns></returns>
        public DepthMap InvertDepthmap( DepthMap dm )
        {
            BitmapSource inverted = dm.GetLevelInverted();
            DepthMap dmi = new DepthMap( inverted );
            dmi.Name = dm.Name + "_inverted";
            myDepthmaps.AddItem( dmi );
            return dmi;
        }

        /// <summary>
        /// Return a new depthmap with adjusted levels
        /// </summary>
        /// <param name="dm"></param>
        /// <returns></returns>
        public DepthMap AdjustDepthmapLevels( DepthMap dm, LevelAdjustments adjustments )
        {
            BitmapSource adjusted = dm.GetLevelAdjusted( adjustments );
            DepthMap dma = new DepthMap( adjusted );
            dma.Name = dm.Name + "_adjusted";
            myDepthmaps.AddItem( dma );
            return dma;
        }

        /// <summary>
        /// Merge multiple depthmaps into a new depthmap by comparing Z values and taking the closest.
        /// Output resolution will always be the same as the primary depthmap
        /// </summary>
        /// <param name="depthmaps"></param>
        /// <returns></returns>
        public DepthMap MergeDepthmaps( BitmapType primary, List<BitmapType> others )
        {
            DepthMap dm = primary as DepthMap;
            if ( primary != null )
            {
                foreach ( var item in others )
                {
                    DepthMap dm2 = item as DepthMap;
                    if ( item != null && item != primary )
                    {
                        DepthMap dmm = dm.MergeWith( dm2 );
                        dmm.Name = dm.Name + "+" + dm2.Name;
                        dm = dmm;                        
                    }                    
                }
                myDepthmaps.AddItem( dm );
                return dm;
            }
            else
            {
                throw new ArgumentException( "No depthmaps to merge" );
            }
        }

        /// <summary>
        /// Event handler for palette item selected
        /// </summary>
        /// <param name="palette"></param>
        /// <param name="thumb"></param>
        private void event_ThumbnailSelected( Palette palette, Thumbnail thumb )
        {
            PreviewItem = thumb;
        }

        public void Populate()
        {
            _depthMapFlat = new DepthMap( 32, 32 );
            _depthMapFlat.Name = "Flat";
            myDepthmaps.AddItem( _depthMapFlat, bCanRemove: false );

            _textureRandomDots = new TextureGreyDots( (int)Options.Separation, (int)Options.Separation );
            _textureRandomDots.Name = "Random Dots";
            myTextures.AddItem( _textureRandomDots, bCanRemove: false );
            _textureRandomColours = new TextureColourDots( (int)Options.Separation, (int)Options.Separation );
            _textureRandomColours.Name = "Random Coloured Dots";
            myTextures.AddItem( _textureRandomColours, bCanRemove: false );

            // Restore any depthmaps saved in the settings (sanity check on max count incase settings get screwed)
            if ( Properties.Settings.Default.Depthmaps != null && Properties.Settings.Default.Depthmaps.Count > 0 && Properties.Settings.Default.Depthmaps.Count < 1000 )
            {
                myDepthmaps.Populate( Properties.Settings.Default.Depthmaps );
            }
            else
            {
                DefaultDepthmaps();
            }

            // Restore any depthmaps saved in the settings
            if ( Properties.Settings.Default.Textures != null && Properties.Settings.Default.Textures.Count > 0 && Properties.Settings.Default.Textures.Count < 1000 )
            {
                myTextures.Populate( Properties.Settings.Default.Textures );
            }
            else
            {
                DefaultTextures();
            }

            // Could do with saving the sterograms as custom objects, with their settings inside... or add custom metadata to the files?
            if ( Properties.Settings.Default.Stereograms != null && Properties.Settings.Default.Stereograms.Count > 0 && Properties.Settings.Default.Stereograms.Count < 1000 )
            {
                myStereograms.Populate( Properties.Settings.Default.Stereograms );
            }
        }

        /// <summary>
        /// Populate Depthmap palette with default resources
        /// </summary>
        public void DefaultDepthmaps()
        {
            myDepthmaps.Clear();

            string[] resources = { @"pack://application:,,,/Images/3D2.png",
                                        @"pack://application:,,,/Images/3dbubbles.png",
                                        @"pack://application:,,,/Images/bumps1.png",
                                        @"pack://application:,,,/Images/oddone3.png",
                                        @"pack://application:,,,/Images/ripple2.png",
                                        @"pack://application:,,,/Images/ripple4.png",
                                        @"pack://application:,,,/Images/sombrero.png",
                                        @"pack://application:,,,/Images/sphere4.png",
                                        @"pack://application:,,,/Images/volcano.png"
                                        };

            myDepthmaps.Populate( resources );
        }

        /// <summary>
        /// Populate Texture palette with default resources
        /// </summary>
        public void DefaultTextures()
        {
            myTextures.Clear();

            string[] resources = { @"pack://application:,,,/Images/chrome_refraction.jpg",
                                       @"pack://application:,,,/Images/chunky_spinach.jpg",
                                       @"pack://application:,,,/Images/dendrite_dance.jpg",
                                       @"pack://application:,,,/Images/distorted_anomaly.jpg",
                                       @"pack://application:,,,/Images/glowing_wildebeast.jpg",
                                       @"pack://application:,,,/Images/NuclearCoral.jpg",
                                       @"pack://application:,,,/Images/thin_tentacles.jpg"
                                     };

            myTextures.Populate( resources );
        }

        /// <summary>
        /// Proxy for displaying an error message
        /// </summary>
        /// <param name="message"></param>
        private void ErrorMessage( string message )
        {
            if ( OnErrorMessage != null )
            {
                OnErrorMessage( message );
            }
        }


    }
}
