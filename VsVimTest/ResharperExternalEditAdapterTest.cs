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
    public class ResharperExternalEditAdapterTest
    {
        sealed class VsTextAdornmentTag : ITag
        {
            internal string myAttributeId;
        }

        private ITextBuffer _textBuffer;
        private ResharperExternalEditAdapter _adapterRaw;
        private IExternalEditAdapter _adapter;
        private MockRepository _factory;

        public void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _adapterRaw = new ResharperExternalEditAdapter();
            _adapter = _adapterRaw;
        }

        [Test]
        public void IsExternalEditMarker_None()
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
        public void IsExternalEditMarker_NormalTagIsNot()
        {
            var tag = _factory.Create<ITag>();
            Assert.IsFalse(_adapter.IsExternalEditTag(tag.Object));
        }

        [Test]
        public void IsExternalEditMarker_RightTagWithAttributes()
        {
            var array = new[]
                            {
                                ResharperExternalEditAdapter.ExternalEditAttribute1,
                                ResharperExternalEditAdapter.ExternalEditAttribute2,
                                ResharperExternalEditAdapter.ExternalEditAttribute3,
                            };
            foreach (var item in array)
            {
                var tag = new VsTextAdornmentTag {myAttributeId = item};
                Assert.IsTrue(_adapter.IsExternalEditTag(tag));
            }
        }

        [Test]
        public void IsExternalEditMarker_RightTagWithWrongAttributes()
        {
            var array = new[] {"dog", "cat"};
            foreach (var item in array)
            {
                var tag = new VsTextAdornmentTag {myAttributeId = item};
                Assert.IsFalse(_adapter.IsExternalEditTag(tag));
            }
        }
    }
}
