using System;
using System.ComponentModel.Composition;
using System.Windows.Threading;
using EditorUtils;

namespace Vim.UI.Wpf.Implementation.Misc
{
    /// <summary>
    /// This class is responsible for handling the timeout of key mappings for a given
    /// IVimBuffer.  If the timeout occurs before the key mapping is completed then the
    /// keys should just be played as normal
    /// </summary>
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class KeyMappingTimeoutHandler : IVimBufferCreationListener
    {
        #region TimerData

        private sealed class TimerData
        {
            private readonly IVimBuffer _vimBuffer;
            private readonly DispatcherTimer _timer;
            private readonly IProtectedOperations _protectedOperations;
            private readonly KeyMappingTimeoutHandler _keyMappingTimeoutHandler;

            internal TimerData(IVimBuffer vimBuffer, IVimProtectedOperations protectedOperations, KeyMappingTimeoutHandler keyMappingTimeoutHandler)
            {
                _protectedOperations = protectedOperations;
                _vimBuffer = vimBuffer;
                _keyMappingTimeoutHandler = keyMappingTimeoutHandler;
                _timer = new DispatcherTimer(DispatcherPriority.Input);
                _timer.Tick += OnTimerTick;
                _vimBuffer.KeyInputProcessed += OnKeyInputProcessed;
                _vimBuffer.KeyInputBuffered += OnKeyInputBuffered;
            }

            internal void Close()
            {
                _timer.Tick -= OnTimerTick;
                _vimBuffer.KeyInputProcessed -= OnKeyInputProcessed;
                _vimBuffer.KeyInputBuffered -= OnKeyInputBuffered;
                _timer.Stop();
            }

            private void OnTimerTick(object sender, EventArgs e)
            {
                try
                {
                    // If the Timer is still enabled then go ahead and process the buffered
                    // KeyInput values
                    if (_timer.IsEnabled)
                    {
                        _vimBuffer.ProcessBufferedKeyInputs();
                    }

                    _keyMappingTimeoutHandler.RaiseTick();
                }
                catch (Exception ex)
                {
                    _protectedOperations.Report(ex);
                }
            }

            /// <summary>
            /// When a KeyInput value is processed then it should stop the timer if it's
            /// currently running.  Actually processing a KeyInput means it wasn't buffered
            /// </summary>
            private void OnKeyInputProcessed(object sender, KeyInputProcessedEventArgs args)
            {
                _timer.Stop();
            }

            private void OnKeyInputBuffered(object sender, KeyInputSetEventArgs args)
            {
                try
                {
                    var globalSettings = _vimBuffer.GlobalSettings;

                    // If 'timeout' is not enabled then ensure the timer is disabled and return.  Ensuring
                    // it's disabled is necessary because the 'timeout' could be disabled in the middle
                    // of processing a key mapping
                    if (!globalSettings.Timeout)
                    {
                        _timer.Stop();
                    }

                    if (_timer.IsEnabled)
                    {
                        _timer.Stop();
                    }

                    _timer.Interval = TimeSpan.FromMilliseconds(globalSettings.TimeoutLength);
                    _timer.Start();
                }
                catch (Exception ex)
                {
                    // Several DispatcherTimer operations including setting the Interval can throw 
                    // so catch them all here
                    _protectedOperations.Report(ex);
                }
            }
        }

        #endregion

        private readonly IVimProtectedOperations _protectedOperations;

        /// <summary>
        /// This event is raised whenever any of the timers for the underlying IVimBuffer values
        /// expires
        /// </summary>
        internal event EventHandler Tick;

        [ImportingConstructor]
        internal KeyMappingTimeoutHandler(IVimProtectedOperations protectedOperations)
        {
            _protectedOperations = protectedOperations;
        }

        internal void OnVimBufferCreated(IVimBuffer vimBuffer)
        {
            var timerData = new TimerData(vimBuffer, _protectedOperations, this);
            vimBuffer.Closed += (sender, e) => timerData.Close();
        }

        private void RaiseTick()
        {
            if (Tick != null)
            {
                Tick(this, EventArgs.Empty);
            }
        }

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            OnVimBufferCreated(vimBuffer);
        }

        #endregion

    }
}
