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

            // Don't use control character notation for ASCII TAB / LF / CR.
            if (i == 9 || i == 10 || i == 13)
            {
                return false;
            }

            return i >= 0 && i <= 31;
        }

        internal static bool TryGetDisplayText(char c, out string text)
        {
            text = null;

            if (!IsDisplayControlChar(c))
            {
                return false;
            }

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
