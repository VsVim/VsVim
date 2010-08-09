using Microsoft.FSharp.Control;
using Vim;

namespace Vim.UnitTest.Mock
{
    class MockTextChangeTracker : ITextChangeTracker
    {
#pragma warning disable 649
        public string CurrentChangeImpl;
#pragma warning restore 649
        public IVimBuffer VimBufferImpl;

        public event FSharpHandler<string> ChangeCompleted;

        public string CurrentChange
        {
            get { return CurrentChangeImpl; }
        }

        public IVimBuffer VimBuffer
        {
            get { return VimBufferImpl; }
        }

        public void RaiseChangeCompleted(string data)
        {
            var e = ChangeCompleted;
            if (e != null)
            {
                e(this, data);
            }
        }
    }
}
