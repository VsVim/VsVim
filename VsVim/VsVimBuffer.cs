using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Microsoft.VisualStudio.Text.Editor;
using VimCore;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.UI.Undo;
using Microsoft.VisualStudio.OLE.Interop;
using System.Windows.Media;
using System.Diagnostics;
using Microsoft.FSharp.Control;
using Microsoft.VisualStudio.Text;

namespace VsVim
{
    internal sealed class VsVimBuffer
    {
        private readonly IWpfTextView m_view;
        private readonly VsVimHost m_host;
        private readonly IVimBuffer m_buffer;
        private readonly VsCommandFilter m_filter;

        internal IVimBuffer VimBuffer
        {
            get { return m_buffer; }
        }

        internal VsVimHost VsVimHost
        {
            get { return m_host; }
        }

        internal VsVimBuffer(IWpfTextView view, IVsTextView shimView, IVsTextLines lines, IUndoHistoryRegistry undoHistory)
        {
            m_view = view;

            var sp = ((IObjectWithSite)shimView).GetServiceProvider();
            m_host = new VsVimHost(sp, undoHistory);
            m_buffer = Factory.CreateVimBuffer(m_host, m_view, lines.GetFileName());
            m_filter = new VsCommandFilter(m_buffer, shimView);

            m_view.GotAggregateFocus += OnGotAggregateFocus;
            m_buffer.SwitchedMode += new FSharpHandler<IMode>(SwitchedMode);
            m_buffer.KeyInputProcessed += new FSharpHandler<KeyInput>(KeyInputProcessed);
            DiagnoseNewLineBug();
            UpdateBrush();
        }

        internal void Close()
        {
            m_view.GotAggregateFocus -= OnGotAggregateFocus;
            m_buffer.SwitchedMode -= new FSharpHandler<IMode>(SwitchedMode);
        }

        /// <summary>
        /// It appears that some properties of the cursor are shared between views.  Most importantly
        /// to us it appears the RegularBrush of the cursor is.  Therefore we have to reset the 
        /// brush whenever we get focus.  Lest we end up with the brush from another VsVimBuffer instance
        /// </summary>
        private void OnGotAggregateFocus(object sender, EventArgs e)
        {
            UpdateBrush();
        }

        private void SwitchedMode(object sender, IMode args)
        {
            UpdateBrush();
        }

        private void KeyInputProcessed(object sender, KeyInput ki)
        {
            UpdateBrush();
        }

        private void UpdateBrush()
        {
            var caret = m_view.Caret;
            var field = caret.GetType().GetField("_caretBrush", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(caret, CalculateCurrentBrush());
        }

        /// <summary>
        /// Calculate the current brush based on the state of the Vim buffer
        /// </summary>
        private Brush CalculateCurrentBrush()
        {
            switch (m_buffer.ModeKind)
            {
                case ModeKind.Insert:
                    return Brushes.Black;
                case ModeKind.Normal:
                case ModeKind.Command:
                    return Brushes.Red;
                default:
                    Debug.Fail("Unrecognized mode kind");
                    return Brushes.Black;
            }
        }


        /// <summary>
        /// There is currently a bug hanging around where the VsVim plugin will force the insertion
        /// of a \r instead of the standard newline ending.  Need to check for that
        /// </summary>
        [Conditional("DEBUG")]
        private void DiagnoseNewLineBug()
        {
            m_view.TextBuffer.Changed += OnTextChanged;
        }

        private void OnTextChanged(object sender, TextContentChangedEventArgs e)
        {
            if (e.Changes.Any(x => x.LineCountDelta != 0))
            {
                foreach (var line in e.After.Lines)
                {
                    var text = line.GetText();
                    Debug.Assert(!text.EndsWith("\n"));
                    Debug.Assert(!text.EndsWith("\r"));

                    var lb = line.GetLineBreakText();
                    Debug.Assert(lb != "\n");
                    Debug.Assert(lb != "\r");
                }
            }
        }

    }
}

