// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading;
using Engine;

namespace Stereogrammer.Model
{
    /// <summary>
    /// Class to generate preview stereograms in a background thread, with an optional delay
    /// On generation, the registered callback function will be called with a reference to the generated stereogram.
    /// Repeated calls to RequestStereogram with different options or delays will pre-empt earlier requests. 
    /// </summary>
    public class StereogramGeneratorAsync : IDisposable
    {
        private readonly BackgroundWorker _worker;
        private StereogramGenerator _generator = null;
        private Options _requestOptions;
        private readonly Action<Stereogram> _callback;

        /// <summary>
        /// Generate a stereogram in a background thread, invoke a callback when it's completed
        /// </summary>
        /// <param name="callback"></param>
        public StereogramGeneratorAsync( Action<Stereogram> callback )
        {
            _worker = new BackgroundWorker();

            _worker.DoWork += new DoWorkEventHandler( generate_DoWork );
            _worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler( generate_Completed );

            _callback = callback;
        }

        public void Dispose()
        {
            if ( _worker.IsBusy )
            {
                _worker.CancelAsync();
            }
        }

        /// <summary>
        /// Request that a preview image be generated in the background after an optional delay.
        /// Stereogram will be fitted to the given width and height, respecting the preserve aspect ratio option.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="millisecondDelay"></param>
        public void RequestStereogram( Options options, long millisecondDelay = 0 )
        {
            _requestOptions = new Options( options );

            if ( millisecondDelay != 0 )
            {
                _requestOptions.time = _requestOptions.time.AddMilliseconds( millisecondDelay );
            }

            // Freeze the input bitmaps so they can be accessed across threads
            // (might be better to make a copy of them and freeze the copy?)
            _requestOptions.DepthMap.Bitmap.Freeze();
            _requestOptions.Texture.Bitmap.Freeze();

            if ( _worker.IsBusy == false )
            {
                _worker.RunWorkerAsync( _requestOptions );
            }
        }

        public double GetProgress()
        {
            if ( _generator != null )
            {
                return ( (double)_generator.GeneratedLines * 100 ) / _generator.NumLines;
            }
            return 0.0;
        }

        /// <summary>
        /// Generate the stereogram on a background thread.  First wait for the specified interval and see if another request pre-empts us.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void generate_DoWork( object sender, DoWorkEventArgs e )
        {
            var worker = sender as BackgroundWorker;

            // Keep generating as long as there's a request in the pipe
            while ( _requestOptions != null )
            {
                Options options = _requestOptions;
                _requestOptions = null;

                if ( options.time.Ticks > DateTime.Now.Ticks )
                {
                    var delta = new TimeSpan( options.time.Ticks - DateTime.Now.Ticks );
                    if ( delta.TotalMilliseconds > 50 )
                    {
                        Thread.Sleep( delta );
                    }
                }

                if ( worker.CancellationPending )
                {
                    e.Cancel = true;
                    return;
                }

                if ( options != null && _requestOptions == null )
                {
                    var old = Thread.CurrentThread.Priority;
                    try
                    {
                        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                        _generator = StereogramGenerator.Get( options );
                        var stereogramBitmap = _generator.Generate();
                        
                        var stereogram = new Stereogram(stereogramBitmap);

                        stereogram.Options = options;
                        stereogram.Name = string.Format( "{0} + {1}", options.DepthMap.Name, options.Texture.Name );

                        e.Result = stereogram;
                    }
                    finally
                    {
                        Thread.CurrentThread.Priority = old;
                    }
                }
            }

        }

        // When the thread completes, we hopefully have a completed stereogram to return to the callback
        private void generate_Completed( object sender, RunWorkerCompletedEventArgs e )
        {
            // First, handle the case where an exception was thrown.
            if ( e.Error != null )
            {
                // Let the callback handle a NULL result
                if ( _callback != null )
                {
                    _callback( null );
                }
            }
            else if ( e.Cancelled )
            {
            }
            else if ( e.Result is Stereogram )
            {
                Stereogram stereogram = (Stereogram)e.Result;

                if ( _callback != null )
                {
                    _callback( stereogram );
                }
            }
        }
    }

}
