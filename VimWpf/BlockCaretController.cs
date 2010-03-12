using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.Modes.Normal;

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
            _buffer.KeyInputProcessed += OnCaretRelatedEvent;
            _buffer.KeyInputReceived += OnCaretRelatedEvent;
            UpdateCaret();
        }

        private void OnCaretRelatedEvent(object sender, object args)
        {
            UpdateCaret();
        }

        private void UpdateCaret()
        {
            var show = false;
            switch (_buffer.ModeKind )
            {
                case ModeKind.Normal:
                    {
                        var mode = _buffer.NormalMode;
                        if (!mode.IsOperatorPending && !mode.IsWaitingForInput)
                        {
                            show = true;
                        }
                        else if ( mode.IsOperatorPending )
                        {
                            show = true;
                        }
                    }
                    break;
                case ModeKind.VisualBlock:
                case ModeKind.VisualCharacter:
                case ModeKind.VisualLine:
                    show = true;
                    break;
            }

            if ( show )
            {
                _blockCaret.Show();
            }
            else
            {
                _blockCaret.Hide();
            }
        }
    }
}
