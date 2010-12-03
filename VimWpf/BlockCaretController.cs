using System;

namespace Vim.UI.Wpf
{
    internal sealed class BlockCaretController
    {
        private readonly IVimBuffer _buffer;
        private readonly IBlockCaret _blockCaret;

        internal BlockCaretController(
            IVimBuffer buffer,
            IBlockCaret blockCaret)
        {
            _buffer = buffer;
            _blockCaret = blockCaret;
            _buffer.SwitchedMode += OnCaretRelatedEvent;
            _buffer.KeyInputStart += OnCaretRelatedEvent;
            _buffer.KeyInputEnd += OnCaretRelatedEvent;
            _buffer.Closed += OnBufferClosed;
            _buffer.Settings.GlobalSettings.SettingChanged += OnSettingsChanged;
            UpdateCaret();
            UpdateCaretOpacity();
        }

        internal void Update()
        {
            UpdateCaret();
        }

        private void OnSettingsChanged(object sender, Setting setting)
        {
            if (setting.Name == GlobalSettingNames.CaretOpacityName)
            {
                UpdateCaretOpacity();
            }
        }

        private void OnCaretRelatedEvent(object sender, object args)
        {
            UpdateCaret();
        }

        private void OnBufferClosed(object sender, EventArgs args)
        {
            _blockCaret.Destroy();

            // Have to remove the global settings event handler here.  The global settings lifetime
            // is tied to IVim and essentially is that of the AppDomain.  Not removing the handler
            // here will lead to a memory leak of this type and the associated IVimBuffer instances
            _buffer.Settings.GlobalSettings.SettingChanged -= OnSettingsChanged;
        }

        private void UpdateCaretOpacity()
        {
            var value = _buffer.Settings.GlobalSettings.CaretOpacity;
            if (value >= 0 && value <= 100)
            {
                var opacity = ((double)value / 100);
                _blockCaret.CaretOpacity = opacity;
            }
        }

        private void UpdateCaret()
        {
            var kind = CaretDisplay.Block;
            switch (_buffer.ModeKind)
            {
                case ModeKind.Normal:
                    {
                        var mode = _buffer.NormalMode;
                        if (mode.IsInReplace)
                        {
                            kind = CaretDisplay.QuarterBlock;
                        }
                        else if (mode.IsOperatorPending)
                        {
                            kind = CaretDisplay.HalfBlock;
                        }
                        else if (mode.IncrementalSearch.InSearch)
                        {
                            kind = CaretDisplay.Invisible;
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
                    kind = CaretDisplay.Block;
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
