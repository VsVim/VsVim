using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vim.UI.Wpf.Implementation.MarkGlyph
{
    [Export(typeof(IMarkDisplayUtil))]
    internal sealed class MarkDisplayUtil : IMarkDisplayUtil
    {
        private string _hideMarks = "";

        public string HideMarks => _hideMarks;
        public event EventHandler HideMarksChanged;

        string IMarkDisplayUtil.HideMarks
        {
            get { return _hideMarks; }
            set
            {
                if (_hideMarks != value)
                {
                    _hideMarks = value;
                    HideMarksChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        event EventHandler IMarkDisplayUtil.HideMarksChanged
        {
            add { HideMarksChanged += value; }
            remove { HideMarksChanged -= value; }
        }
    }
}
