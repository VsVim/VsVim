using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using System;
using System.Collections.Generic;
using Vim.UnitTest;
using Vim.VisualStudio.Implementation.ExternalEdit;
using Vim.VisualStudio.Implementation.ReSharper;
using Vim.VisualStudio.UnitTest.Mock;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class ReSharperUtilTest : VimTestBase
    {
        sealed class VsTextAdornmentTag : ITag
        {
            internal string myAttributeId;
        }

        private ITextBuffer _textBuffer;
        private ReSharperUtil _reSharperUtilRaw;
        private IExternalEditAdapter _reSharperUtil;
        private MockRepository _factory;

        public void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _reSharperUtilRaw = new ReSharperUtil(isResharperInstalled: true);
            _reSharperUtil = _reSharperUtilRaw;
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
                    Assert.False(_reSharperUtil.IsExternalEditMarker(marker.Object));
                }
            }

            [Fact]
            public void NormalTagIsNot()
            {
                Create("");
                var tag = _factory.Create<ITag>();
                Assert.False(_reSharperUtil.IsExternalEditTag(tag.Object));
            }

            [Fact]
            public void RightTagWithAttributes()
            {
                Create("");
                _reSharperUtilRaw.SetReSharperVersion(ReSharperVersion.Version7AndEarlier);
                var array = new[]
                            {
                                ReSharperEditTagDetectorBase.ExternalEditAttribute1,
                                ReSharperEditTagDetectorBase.ExternalEditAttribute2,
                                ReSharperEditTagDetectorBase.ExternalEditAttribute3,
                            };
                foreach (var item in array)
                {
                    var tag = new VsTextAdornmentTag { myAttributeId = item };
                    Assert.True(_reSharperUtil.IsExternalEditTag(tag));
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
                    Assert.False(_reSharperUtil.IsExternalEditTag(tag));
                }
            }
        }

        public sealed class MiscTest : ReSharperUtilTest
        {
            /// <summary>
            /// The Laszy property in imported collections can throw an exception due to composition
            /// failure.  Make sure this exception doesn't bring down the IsInterested check
            /// </summary>
            [Fact]
            public void Issue1381()
            {
                Create();
                var lazy = new Lazy<ITaggerProvider>(() => { throw new Exception(); });
                var list = new List<Lazy<ITaggerProvider>>();
                list.Add(lazy);
                _reSharperUtilRaw.TaggerProviders = list;
                var textView = CreateTextView();
                ITagger<ITag> tagger;
                Assert.False(_reSharperUtil.IsInterested(textView, out tagger));
            }
        }
    }
}
