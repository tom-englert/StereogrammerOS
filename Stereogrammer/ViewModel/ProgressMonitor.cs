using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;
using System.ComponentModel;
using System.Diagnostics;

namespace Stereogrammer.ViewModel
{
    public class ProgressMonitor :IDisposable
    {
        public delegate float UpdateProgress();
        public delegate void ReportStatus( double progress );
        public delegate void OnStart();
        public delegate void OnCompletion();

        private UpdateProgress _updateProgress = null;
        private readonly ReportStatus _reportStatus;
        private readonly OnStart _onStart;
        private readonly OnCompletion _onCompletion;

        private double _progress = 0.0;

        private readonly BackgroundWorker _monitor;

        /// <summary>
        /// Create a progress monitor with delegates for reporting status and completion
        /// </summary>
        /// <param name="report"></param>
        /// <param name="complete"></param>
        public ProgressMonitor( ReportStatus report, OnStart start = null, OnCompletion complete = null )
        {
            _reportStatus = report;
            _onStart = start;
            _onCompletion = complete;

            _monitor = new BackgroundWorker();
            _monitor.DoWork += new DoWorkEventHandler( monitor_DoWork );
            _monitor.RunWorkerCompleted += new RunWorkerCompletedEventHandler( monitor_Completed );
            _monitor.ProgressChanged += new ProgressChangedEventHandler( monitor_ReportProgress );
            _monitor.WorkerReportsProgress = true;
            _monitor.WorkerSupportsCancellation = true;
        }

        public void Dispose()
        {
            _monitor.Dispose();
        }

        /// <summary>
        /// Start monitoring a process
        /// </summary>
        /// <param name="update"></param>
        public void MonitorProgress( UpdateProgress update )
        {
            Debug.Assert( update != null );
            _updateProgress = update;

            if ( _onStart != null )
            {
                _onStart();
            }

            if ( false == _monitor.IsBusy )
            {
                _monitor.RunWorkerAsync();
            }
        }

        /// <summary>
        /// Stop monitoring a process
        /// </summary>
        public void EndMonitoring()
        {
            if ( _monitor.IsBusy )
            {
                _monitor.CancelAsync();
                _updateProgress = null;
            }
        }

        private void monitor_DoWork( object sender, DoWorkEventArgs e )
        {
            var monitor = (BackgroundWorker)sender;

            while ( !monitor.CancellationPending && _updateProgress != null )
            {
                monitor.ReportProgress(0);

                Thread.Sleep( 50 );
            }
        }

        private void monitor_ReportProgress( object sender, ProgressChangedEventArgs e )
        {
            if ( _updateProgress != null )
            {
                _progress = _updateProgress();

                if ( _reportStatus != null )
                {
                    _reportStatus( _progress );
                }                
            }
        }

        private void monitor_Completed( object sender, RunWorkerCompletedEventArgs e )
        {
            if ( _onCompletion != null )
            {
                _onCompletion();
            }
        }
    }
}
