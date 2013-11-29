using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Vim.UnitTest;
using VsVim.Implementation.ExternalEdit;
using VsVim.Implementation.ReSharper;
using VsVim.UnitTest.Mock;
using Xunit;

namespace VsVim.UnitTest
{
    public abstract class ReSharperUtilTest : VimTestBase
    {
        sealed class VsTextAdornmentTag : ITag
        {
            internal string myAttributeId;
        }

        private ITextBuffer _textBuffer;
        private ReSharperUtil _adapterRaw;
        private IExternalEditAdapter _adapter;
        private MockRepository _factory;

        public void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _adapterRaw = new ReSharperUtil(true);
            _adapter = _adapterRaw;
        }

        public sealed class IsExternalEditMarkerTest : ReSharperUtilTest
        {
            /// <summary>
            /// Ensure that the R# adapter doesn't pick up on IVsTextMarker instances
            /// </summary>
            [Fact]
            public void None()
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
            public void NormalTagIsNot()
            {
                Create("");
                var tag = _factory.Create<ITag>();
                Assert.False(_adapter.IsExternalEditTag(tag.Object));
            }

            [Fact]
            public void RightTagWithAttributes()
            {
                Create("");
                _adapterRaw.SetReSharperVersion(ReSharperVersion.Version7AndEarlier);
                var array = new[]
                            {
                                ReSharperEditTagDetectorBase.ExternalEditAttribute1,
                                ReSharperEditTagDetectorBase.ExternalEditAttribute2,
                                ReSharperEditTagDetectorBase.ExternalEditAttribute3,
                            };
                foreach (var item in array)
                {
                    var tag = new VsTextAdornmentTag { myAttributeId = item };
                    Assert.True(_adapter.IsExternalEditTag(tag));
                }
            }

            [Fact]
            public void RightTagWithWrongAttributes()
            {
                Create("");
                var array = new[] { "dog", "cat" };
                foreach (var item in array)
                {
                    var tag = new VsTextAdornmentTag { myAttributeId = item };
                    Assert.False(_adapter.IsExternalEditTag(tag));
                }
            }
        }
    }
}
