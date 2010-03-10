using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            _buffer.SwitchedMode += new Microsoft.FSharp.Control.FSharpHandler<IMode>(OnSwitchMode);
            UpdateCaretBasedOnMode();
        }

        private void OnSwitchMode(object sender, IMode args)
        {
            UpdateCaretBasedOnMode();
        }

        private void UpdateCaretBasedOnMode()
        {
            if (ModeKind.Normal == _buffer.ModeKind)
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
