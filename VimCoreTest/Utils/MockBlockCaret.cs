using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;
using Microsoft.VisualStudio.Text.Editor;

namespace VimCoreTest.Utils
{
    internal sealed class MockBlockCaret : IBlockCaret
    {
        internal IWpfTextView RawTextView;
        internal int DestroyCount;
        internal int HideCount;
        internal int ShowCount;
        internal bool isShown;

        internal MockBlockCaret(IWpfTextView view = null)
        {
            RawTextView = view;
        }
        
        public void Destroy()
        {
            DestroyCount++;
        }

        public void Hide()
        {
            HideCount++;
        }

        public void Show()
        {
            ShowCount++;
        }

        public bool IsShown { get { return isShown; } }

        public Microsoft.VisualStudio.Text.Editor.ITextView TextView
        {
            get { return RawTextView; }
        }
    }
}
