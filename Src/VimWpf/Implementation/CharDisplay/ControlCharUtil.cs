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
            int i = (int)c;
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
            switch (i)
            {
                case 0: text = "^@"; break;
                case 1: text = "^A"; break;
                case 2: text = "^B"; break;
                case 3: text = "^C"; break;
                case 4: text = "^D"; break;
                case 5: text = "^E"; break;
                case 6: text = "^F"; break;
                case 7: text = "^G"; break;
                case 8: text = "^H"; break;
                case 9: text = "^I"; break;
                case 10: text = "^J"; break;
                case 11: text = "^K"; break;
                case 12: text = "^L"; break;
                case 13: text = "^M"; break;
                case 14: text = "^N"; break;
                case 15: text = "^O"; break;
                case 16: text = "^P"; break;
                case 17: text = "^Q"; break;
                case 18: text = "^R"; break;
                case 19: text = "^S"; break;
                case 20: text = "^T"; break;
                case 21: text = "^U"; break;
                case 22: text = "^V"; break;
                case 23: text = "^W"; break;
                case 24: text = "^X"; break;
                case 25: text = "^Y"; break;
                case 26: text = "^Z"; break;
                case 27: text = "^["; break;
                case 28: text = "^\\"; break;
                case 29: text = "^]"; break;
                case 30: text = "^^"; break;
                case 31: text = "^_"; break;
            }

            return text != null;
        }

        private void RaiseDisplayControlCharsChanged()
        {
            EventHandler e = _displayControlCharsChanged;
            if (e != null)
            {
                e(this, EventArgs.Empty);
            }
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
