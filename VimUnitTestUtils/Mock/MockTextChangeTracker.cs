using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;

namespace Vim.UnitTest.Mock
{
    class MockTextChangeTracker : ITextChangeTracker
    {
#pragma warning disable 649
        public FSharpOption<TextChange> CurrentChangeImpl;
#pragma warning restore 649
        public IVimBuffer VimBufferImpl;

        public event FSharpHandler<TextChange> ChangeCompleted;

        public FSharpOption<TextChange> CurrentChange
        {
            get { return CurrentChangeImpl; }
        }

        public IVimBuffer VimBuffer
        {
            get { return VimBufferImpl; }
        }

        public void RaiseChangeCompleted(string data)
        {
            RaiseChangeCompleted(TextChange.NewInsert(data));
        }

        public void RaiseChangeCompleted(TextChange change)
        {
            var e = ChangeCompleted;
            if (e != null)
            {
                e(this, change);
            }
        }
    }
}
