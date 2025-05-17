using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace VimApp.Implementation
{
    /// <summary>
    /// Provides options for the Vim application.
    /// </summary>
    [Export(typeof(IVimAppOptions))]
    internal sealed class VimAppOptions : IVimAppOptions
    {
        private bool _displayNewLines;

        /// <summary>
        /// Gets or sets a value indicating whether to display new lines.
        /// </summary>
        public bool DisplayNewLines
        {
            get { return _displayNewLines; }
            set
            {
                if (value != _displayNewLines)
                {
                    _displayNewLines = value;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Occurs when an option value has changed.
        /// </summary>
        public event EventHandler Changed;

        internal VimAppOptions()
        {

        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
