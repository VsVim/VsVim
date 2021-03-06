﻿using System;
using System.ComponentModel.Composition;
using System.Windows.Threading;

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
        private readonly static object Key = new object();

        #region TimerData

        internal sealed class TimerData
        {
            private readonly IVimBuffer _vimBuffer;
            private readonly DispatcherTimer _timer;
            private readonly IProtectedOperations _protectedOperations;
            private readonly KeyMappingTimeoutHandler _keyMappingTimeoutHandler;

            /// <summary>
            /// This event is raised whenever the timer fires
            /// </summary>
            internal event EventHandler Tick;

            internal TimerData(IVimBuffer vimBuffer, IProtectedOperations protectedOperations, KeyMappingTimeoutHandler keyMappingTimeoutHandler)
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

                    Tick?.Invoke(this, EventArgs.Empty);
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
                        return;
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

        private readonly IProtectedOperations _protectedOperations;

        [ImportingConstructor]
        internal KeyMappingTimeoutHandler(IProtectedOperations protectedOperations)
        {
            _protectedOperations = protectedOperations;
        }

        internal static bool TryGetTimerData(IVimBuffer vimBuffer, out TimerData timerData) =>
            vimBuffer.Properties.TryGetProperty<TimerData>(Key, out timerData);

        internal void OnVimBufferCreated(IVimBuffer vimBuffer)
        {
            var timerData = new TimerData(vimBuffer, _protectedOperations, this);
            vimBuffer.Properties.AddProperty(Key, timerData);
            vimBuffer.Closed += (sender, e) =>
            {
                timerData.Close();
                vimBuffer.Properties.RemoveProperty(Key);
            };
        }

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            OnVimBufferCreated(vimBuffer);
        }

        #endregion
    }
}
