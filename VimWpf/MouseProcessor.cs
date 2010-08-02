using System;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// The purpose of this component is to manage the transition between the start of a 
    /// selection to the completion.  Visual Mode must be started at the start of the selection
    /// but not completely enabled until the selection has completed
    /// </summary>
    public sealed class MouseProcessor : Microsoft.VisualStudio.Text.Editor.MouseProcessorBase
    {
        private readonly IVimBuffer _buffer;
        private readonly IMouseDevice _mouseDevice;
        private readonly ITextSelection _selection;

        private bool _isSelectionChanging = false;

        /// <summary>
        /// Is the selection currently being updated by the mouse
        /// </summary>
        public bool IsSelectionChanging
        {
            get { return _isSelectionChanging; }
            set { _isSelectionChanging = value; }
        }

        private bool IsAnyVisualMode
        {
            get
            {
                switch (_buffer.ModeKind)
                {
                    case ModeKind.VisualLine:
                    case ModeKind.VisualCharacter:
                    case ModeKind.VisualBlock:
                        return true;
                    default:
                        return false;
                }
            }
        }

        private bool ShouldHandleEvents
        {
            get { return _buffer.ModeKind != ModeKind.Disabled; }
        }

        public MouseProcessor(IVimBuffer buffer, IMouseDevice mouseDevice)
        {
            _buffer = buffer;
            _selection = buffer.TextView.Selection;
            _mouseDevice = mouseDevice;
            _selection.SelectionChanged += OnSelectionChanged;
        }

        public MouseProcessor(IVimBuffer buffer)
            : this(buffer, new Implementation.MouseDeviceImpl())
        {
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            if (!ShouldHandleEvents)
            {
                return;
            }

            if (!IsSelectionChanging && !_selection.IsEmpty && !IsAnyVisualMode)
            {
                // Actually process the selection event during background processing.  The editor commonly
                // implements operations as a selection + edit combination.  Delete Previous word for instance
                // works this way.  By processing later we can see if this selection is persisted
                Action func = () =>
                {
                    if (!IsSelectionChanging && !_selection.IsEmpty && !IsAnyVisualMode)
                    {
                        var modeKind = _selection.Mode == TextSelectionMode.Stream
                            ? ModeKind.VisualCharacter
                            : ModeKind.VisualBlock;

                        var mode = (IVisualMode)(_buffer.SwitchMode(modeKind, ModeArgument.None));

                        // If the left mouse button is pressed then we are in the middle of 
                        // a mouse selection event and need to record the data
                        if (_mouseDevice.IsLeftButtonPressed)
                        {
                            _isSelectionChanging = true;
                        }
                    }
                };

                Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    func);
            }

        }

        public override void PostprocessMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!ShouldHandleEvents)
            {
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                if (IsSelectionChanging)
                {
                    // Just completed a selection event.  Nothing to worry about
                    _isSelectionChanging = false;
                }
                else if (IsAnyVisualMode)
                {
                    // Mouse was clicked and we are in visual mode.  Switch out to the previous
                    // mode.  Do this at background so it doesn't interfer with other processing
                    Action func = () => _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                    Dispatcher.CurrentDispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        func);
                }
            }
        }
    }
}
