using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using NUnit.Framework;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using VsVim.ExternalEdit;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class SnippetExternalEditAdapterTest
    {
        private ITextBuffer _textBuffer;
        private SnippetExternalEditAdapter _adapterRaw;
        private IExternalEditAdapter _adapter;
        private MockRepository _factory;

        public void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _adapterRaw = new SnippetExternalEditAdapter();
            _adapter = _adapterRaw;
        }

        [Test]
        public void IsExternalEditTag_NoneMatter()
        {
            Create();
            Assert.IsFalse(_adapter.IsExternalEditTag(_factory.Create<ITag>().Object));
        }

        [Test]
        public void IsExternalEditMarker_PredefinedTypeIsNotSnippetRelated()
        {
            Create("cat", "dog", "tree");
            for (var i = 0; i < (int)(MARKERTYPE.DEF_MARKER_COUNT); i++)
            {
                var span = _textBuffer.GetLineRange(0).Extent.ToTextSpan();
                var marker = MockObjectFactory.CreateVsTextLineMarker(span, i, _factory);
                Assert.IsFalse(_adapterRaw.IsExternalEditMarker(marker.Object));
            }
        }

        [Test]
        public void IsExternalEditMarker_SnippetTypesAreExternalEdits()
        {
            Create("cat", "dog", "tree");
            var array = new[] {15, 16, 26};
            foreach (var item in array)
            {
                var span = _textBuffer.GetLineRange(0).Extent.ToTextSpan();
                var marker = MockObjectFactory.CreateVsTextLineMarker(span, item, _factory);
                Assert.IsTrue(_adapterRaw.IsExternalEditMarker(marker.Object));
            }
        }

        [Test]
        public void IsExternalEditMarker_OtherTypesAreNotExternalEdits()
        {
            Create("cat", "dog", "tree");
            var array = new[] {150, 160, 260, 25};
            foreach (var item in array)
            {
                var span = _textBuffer.GetLineRange(0).Extent.ToTextSpan();
                var marker = MockObjectFactory.CreateVsTextLineMarker(span, item, _factory);
                Assert.IsFalse(_adapterRaw.IsExternalEditMarker(marker.Object));
            }
        }
    }
}
