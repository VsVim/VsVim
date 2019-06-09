using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace Vim.UI.Wpf.Implementation.CharDisplay
{
    [Export(typeof(IControlCharUtil))]
    internal sealed class ControlCharUtil : IControlCharUtil
    {
        private bool _displayControlChars = true;
        private EventHandler _displayControlCharsChanged;

        private bool DisplayControlChars
        {
            get { return _displayControlChars; }
            set
            {
                if (value != _displayControlChars)
                {
                    _displayControlChars = value;
                    RaiseDisplayControlCharsChanged();
                }
            }
        }

        internal static bool IsDisplayControlChar(char c)
        {
            var i = (int)c;
            return IsRelevant(i);
        }

        internal static bool IsRelevant(int i)
        {
            return i <= 31;
        }

        internal static bool TryGetDisplayText(char c, out string text)
        {
            var i = (int)c;
            if (!IsRelevant(i))
            {
                text = null;
                return false;
            }

            // There is an intentional gap here from 9 - 13 inclusive.  These represent characters like tab, newline,
            // etc ...  which shouldn't be special cased in display by VsVim
            if (i >= 9 && i <= 13)
            {
                text = null;
                return false;
            }

            return TryGetDisplayText(i, out text);
        }

        internal static bool TryGetDisplayText(int i, out string text)
        {
            text = null;
            var c = (char)i;
            if (char.IsControl(c))
            {
                text = StringUtil.GetDisplayString(c.ToString());
                return true;
            }

            return false;
        }

        private void RaiseDisplayControlCharsChanged()
        {
            _displayControlCharsChanged?.Invoke(this, EventArgs.Empty);
        }

        #region IControlCharUtil

        bool IControlCharUtil.DisplayControlChars
        {
            get { return DisplayControlChars; }
            set { DisplayControlChars = value; }
        }

        event EventHandler IControlCharUtil.DisplayControlCharsChanged
        {
            add { _displayControlCharsChanged += value; }
            remove { _displayControlCharsChanged -= value; }
        }

        bool IControlCharUtil.IsDisplayControlChar(char c)
        {
            return IsDisplayControlChar(c);
        }

        bool IControlCharUtil.TryGetDisplayText(char c, out string text)
        {
            return TryGetDisplayText(c, out text);
        }

        #endregion
    }
}
