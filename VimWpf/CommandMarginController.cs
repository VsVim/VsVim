using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.UI.Wpf.Properties;
using System.Collections.ObjectModel;
using System.Windows;

namespace Vim.UI.Wpf
{
    internal sealed class CommandMarginController
    {
        private readonly IVimBuffer _buffer;
        private readonly CommandMarginControl _margin;
        private readonly ReadOnlyCollection<Lazy<IOptionsProviderFactory>> _optionsProviderFactory;
        private bool _ignoreNextKeyProcessedEvent;

        internal CommandMarginController(IVimBuffer buffer, CommandMarginControl control, IEnumerable<Lazy<IOptionsProviderFactory>> optionsProviderFactory)
        {
            _buffer = buffer;
            _margin = control;
            _optionsProviderFactory = optionsProviderFactory.ToList().AsReadOnly();

            _buffer.SwitchedMode += OnSwitchMode;
            _buffer.KeyInputProcessed += OnKeyInputProcessed;
            _buffer.KeyInputReceived += OnKeyInputReceived;
            _buffer.StatusMessage += OnStatusMessage;
            _buffer.ErrorMessage += OnErrorMessage;
            _margin.OptionsClicked += OnOptionsClicked;
        }

        private void OnSwitchMode(object sender, IMode mode)
        {
            switch (mode.ModeKind)
            {
                case ModeKind.Normal:
                case ModeKind.Command:
                    _margin.StatusLine = String.Empty;
                    break;
                case ModeKind.Insert:
                    _margin.StatusLine = Resources.InsertBanner;
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
                case ModeKind.Disabled:
                    _margin.StatusLine = _buffer.DisabledMode.HelpMessage;
                    break;
                default:
                    _margin.StatusLine = String.Empty;
                    break;
            }
        }

        private void OnKeyInputProcessed(object sender, Tuple<KeyInput, ProcessResult> tuple)
        {
            if (_ignoreNextKeyProcessedEvent)
            {
                _ignoreNextKeyProcessedEvent = false;
                return;
            }

            switch (_buffer.ModeKind)
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
                            _margin.StatusLine = "/" + data.Text.RawText;
                        }
                        else
                        {
                            _margin.StatusLine = mode.Command;
                        }
                    }
                    break;
                case ModeKind.Disabled:
                    _margin.StatusLine = _buffer.DisabledMode.HelpMessage;
                    break;
            }
        }

        private void OnKeyInputReceived(object sender, KeyInput input)
        {

        }

        private void OnStatusMessage(object sender, string message)
        {
            _margin.StatusLine = message;
            _ignoreNextKeyProcessedEvent = _buffer.IsProcessingInput;
        }

        private void OnErrorMessage(object sender, string message)
        {
            _margin.StatusLine = message;
            _ignoreNextKeyProcessedEvent = _buffer.IsProcessingInput;
        }

        private void OnOptionsClicked(object sender, EventArgs e)
        {
            var provider = _optionsProviderFactory.Select(x => x.Value.CreateOptionsProvider()).Where(x => x != null).FirstOrDefault();
            if (provider != null)
            {
                provider.ShowDialog(_buffer);
            }
            else
            {
                MessageBox.Show("No options provider available");
            }
        }
    }
}
