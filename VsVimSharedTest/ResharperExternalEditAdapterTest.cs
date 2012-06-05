using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Vim.UnitTest;
using VsVim.ExternalEdit;
using VsVim.UnitTest.Mock;
using Xunit;

namespace VsVim.UnitTest
{
    public sealed class ResharperExternalEditAdapterTest : VimTestBase
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
            _textBuffer = CreateTextBuffer(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _adapterRaw = new ResharperExternalEditAdapter(true);
            _adapter = _adapterRaw;
        }

        /// <summary>
        /// Ensure that the R# adapter doesn't pick up on IVsTextMarker instances
        /// </summary>
        [Fact]
        public void IsExternalEditMarker_None()
        {
            Create("cat", "dog", "tree");
            for (var i = 0; i < (int)(MARKERTYPE.DEF_MARKER_COUNT); i++)
            {
                var span = _textBuffer.GetLineRange(0).Extent.ToTextSpan();
                var marker = MockObjectFactory.CreateVsTextLineMarker(span, i, _factory);
                Assert.False(_adapter.IsExternalEditMarker(marker.Object));
            }
        }

        [Fact]
        public void IsExternalEditMarker_NormalTagIsNot()
        {
            Create("");
            var tag = _factory.Create<ITag>();
            Assert.False(_adapter.IsExternalEditTag(tag.Object));
        }

        [Fact]
        public void IsExternalEditMarker_RightTagWithAttributes()
        {
            Create("");
            var array = new[]
                            {
                                ResharperExternalEditAdapter.ExternalEditAttribute1,
                                ResharperExternalEditAdapter.ExternalEditAttribute2,
                                ResharperExternalEditAdapter.ExternalEditAttribute3,
                            };
            foreach (var item in array)
            {
                var tag = new VsTextAdornmentTag {myAttributeId = item};
                Assert.True(_adapter.IsExternalEditTag(tag));
            }
        }

        [Fact]
        public void IsExternalEditMarker_RightTagWithWrongAttributes()
        {
            Create("");
            var array = new[] {"dog", "cat"};
            foreach (var item in array)
            {
                var tag = new VsTextAdornmentTag {myAttributeId = item};
                Assert.False(_adapter.IsExternalEditTag(tag));
            }
        }
    }
}
