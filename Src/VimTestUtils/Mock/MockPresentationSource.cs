using System.Windows;
using System.Windows.Media;

namespace Vim.UnitTest.Mock
{
    public class MockPresentationSource : PresentationSource
    {
        public Visual RootVisualImpl;

        protected override System.Windows.Media.CompositionTarget GetCompositionTargetCore()
        {
            throw new System.NotImplementedException();
        }

        public override bool IsDisposed
        {
            get { return false; }
        }

        public override Visual RootVisual
        {
            get { return RootVisualImpl; }
            set { RootVisualImpl = value; }
        }
    }
}
