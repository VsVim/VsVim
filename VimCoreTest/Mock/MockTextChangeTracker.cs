using Microsoft.FSharp.Control;
using Vim;

namespace VimCore.Test.Mock
{
    class MockTextChangeTracker : ITextChangeTracker
    {
        public string CurrentChangeImpl;
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
