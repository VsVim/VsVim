using System;
using Vim.Extensions;

namespace Vim.UI.Wpf.Implementation.BlockCaret
{
    internal sealed class BlockCaretController
    {
        private readonly IVimBuffer _buffer;
        private readonly IBlockCaret _blockCaret;
        private readonly IVimGlobalSettings _globalSettings;

        internal BlockCaretController(
            IVimBuffer buffer,
            IBlockCaret blockCaret)
        {
            _buffer = buffer;
            _blockCaret = blockCaret;
            _globalSettings = _buffer.LocalSettings.GlobalSettings;
            _buffer.SwitchedMode += OnCaretRelatedEvent;
            _buffer.KeyInputStart += OnCaretRelatedEvent;
            _buffer.KeyInputEnd += OnCaretRelatedEvent;
            _buffer.Closed += OnBufferClosed;
            _globalSettings.SettingChanged += OnSettingsChanged;
            UpdateCaretDisplay();
            UpdateCaretOpacity();
        }

        internal void Update()
        {
            UpdateCaretDisplay();
        }

        private void OnSettingsChanged(object sender, SettingEventArgs args)
        {
            var setting = args.Setting;
            if (setting.Name == GlobalSettingNames.CaretOpacityName)
            {
                UpdateCaretOpacity();
            }
            else if (setting.Name == GlobalSettingNames.SelectionName)
            {
                UpdateCaretDisplay();
            }
        }

        private void OnCaretRelatedEvent(object sender, object args)
        {
            UpdateCaretDisplay();
        }

        private void OnBufferClosed(object sender, EventArgs args)
        {
            _blockCaret.Destroy();

            // Have to remove the global settings event handler here.  The global settings lifetime
            // is tied to IVim and essentially is that of the AppDomain.  Not removing the handler
            // here will lead to a memory leak of this type and the associated IVimBuffer instances
            _globalSettings.SettingChanged -= OnSettingsChanged;
        }

        private void UpdateCaretOpacity()
        {
            var value = _buffer.LocalSettings.GlobalSettings.CaretOpacity;
            if (value >= 0 && value <= 100)
            {
                var opacity = ((double)value / 100);
                _blockCaret.CaretOpacity = opacity;
            }
        }

        /// <summary>
        /// Update the caret display based on the current state of Vim
        /// </summary>
        private void UpdateCaretDisplay()
        {
            // The caret should be invisible during incremental search no matter what mode we are
            // in
            if (_buffer.IncrementalSearch.InSearch)
            {
                _blockCaret.CaretDisplay = CaretDisplay.Invisible;
                return;
            }

            var kind = CaretDisplay.Block;
            switch (_buffer.ModeKind)
            {
                case ModeKind.Normal:
                    {
                        var mode = _buffer.NormalMode;
                        if (mode.InReplace)
                        {
                            kind = CaretDisplay.QuarterBlock;
                        }
                        else if (mode.KeyRemapMode.IsOperatorPending)
                        {
                            kind = CaretDisplay.HalfBlock;
                        }
                        else
                        {
                            kind = CaretDisplay.Block;
                        }
                    }
                    break;
                case ModeKind.VisualBlock:
                case ModeKind.VisualCharacter:
                case ModeKind.VisualLine:
                case ModeKind.SelectBlock:
                case ModeKind.SelectCharacter:
                case ModeKind.SelectLine:

                    // In visual mode we change the caret based on what the selection mode
                    // is
                    kind = _globalSettings.IsSelectionInclusive
                       ? CaretDisplay.Block
                       : CaretDisplay.Select;
                    break;
                case ModeKind.Command:
                case ModeKind.SubstituteConfirm:
                    kind = CaretDisplay.Invisible;
                    break;
                case ModeKind.Insert:
                case ModeKind.ExternalEdit:
                    kind = CaretDisplay.NormalCaret;
                    break;
                case ModeKind.Disabled:
                    kind = CaretDisplay.NormalCaret;
                    break;
                case ModeKind.Replace:
                    kind = CaretDisplay.QuarterBlock;
                    break;
            }

            _blockCaret.CaretDisplay = kind;
        }
    }
}
