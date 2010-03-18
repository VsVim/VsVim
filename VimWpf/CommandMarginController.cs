using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.UI.Wpf.Properties;

namespace Vim.UI.Wpf
{
    internal sealed class CommandMarginController
    {
        private readonly IVimBuffer _buffer;
        private readonly CommandMarginControl _margin;

        internal CommandMarginController(IVimBuffer buffer, CommandMarginControl control)
        {
            _buffer = buffer;
            _margin = control;

            _buffer.SwitchedMode += OnSwitchMode;
            _buffer.KeyInputProcessed += OnKeyInputProcessed;
            _buffer.KeyInputReceived += OnKeyInputReceived;
        }

        private void UpdateStatusLine()
        {

        }

        private void OnSwitchMode(object sender, IMode mode)
        {
            _margin.RightStatusLine = String.Empty;
            switch (mode.ModeKind)
            {
                case ModeKind.Normal:
                case ModeKind.Command:
                case ModeKind.Insert:
                    _margin.StatusLine = String.Empty;
                    break;
                case ModeKind.VisualBlock:
                    _margin.StatusLine = Resources.VisualBlockBanner;
                    break;
                case ModeKind.VisualCharacter:
                    _margin.StatusLine = Resources.VisualCharacterBanner;
                    break;
                case ModeKind.VisualLine:
                    _margin.StatusLine = Resources.VisualLineBanner;
                    break;
                default:
                    _margin.StatusLine = String.Empty;
                    break;
            }
        }

        private void OnKeyInputProcessed(object sender, KeyInput input)
        {
            switch ( _buffer.ModeKind )
            {
                case ModeKind.Command:
                    _margin.StatusLine = ":" + _buffer.CommandMode.Command;
                    break;
                case ModeKind.Normal:
                    {
                        var mode = _buffer.NormalMode;
                        var search = mode.IncrementalSearch;
                        if (search.InSearch && search.CurrentSearch.HasValue())
                        {
                            var data = search.CurrentSearch.Value;
                            _margin.StatusLine = "/" + data.Pattern;
                        }
                        else
                        {
                            _margin.StatusLine = mode.Command;
                        }
                    }
                    break;
            }
        }

        private void OnKeyInputReceived(object sender, KeyInput input)
        {

        }
    }
}
