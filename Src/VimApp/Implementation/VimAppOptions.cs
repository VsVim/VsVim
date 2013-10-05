using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace VimApp.Implementation
{
    [Export(typeof(IVimAppOptions))]
    internal sealed class VimAppOptions : IVimAppOptions
    {
        private bool _displayNewLines;

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

        public event EventHandler Changed;

        internal VimAppOptions()
        {

        }

        private void RaiseChanged()
        {
            var e = Changed;
            if (e != null)
            {
                e(this, EventArgs.Empty);
            }
        }
    }
}
