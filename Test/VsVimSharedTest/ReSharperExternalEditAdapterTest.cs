using Vim.EditorHost;
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
    public abstract class ReSharperExternalEditAdapterTest : VimTestBase
    {
        private sealed class VsTextAdornmentTag : ITag
        {
            internal string myAttributeId;
        }

        private ITextBuffer _textBuffer;
        private ReSharperExternalEditAdapter _editAdapterRaw;
        private IExternalEditAdapter _editAdapter;
        private MockRepository _factory;

        public void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _editAdapterRaw = new ReSharperExternalEditAdapter(new ReSharperUtil(true), null);
            _editAdapter = _editAdapterRaw;
        }

        public sealed class IsExternalEditMarkerTest : ReSharperExternalEditAdapterTest
        {
            /// <summary>
            /// Ensure that the R# adapter doesn't pick up on IVsTextMarker instances
            /// </summary>
            [WpfFact]
            public void None()
            {
                Create("cat", "dog", "tree");
                for (var i = 0; i < (int)(MARKERTYPE.DEF_MARKER_COUNT); i++)
                {
                    var span = _textBuffer.GetLineRange(0).Extent.ToTextSpan();
                    var marker = MockObjectFactory2.CreateVsTextLineMarker(span, i, _factory);
                    Assert.False(_editAdapter.IsExternalEditMarker(marker.Object));
                }
            }

            [WpfFact]
            public void NormalTagIsNot()
            {
                Create("");
                var tag = _factory.Create<ITag>();
                Assert.False(_editAdapter.IsExternalEditTag(tag.Object));
            }

            [WpfFact]
            public void RightTagWithAttributes()
            {
                Create("");
                _editAdapterRaw.ResetForVersion(ReSharperVersion.Version7AndEarlier);

                var array = new[]
                            {
                                ReSharperEditTagDetectorBase.ExternalEditAttribute1,
                                ReSharperEditTagDetectorBase.ExternalEditAttribute2,
                                ReSharperEditTagDetectorBase.ExternalEditAttribute3,
                            };
                foreach (var item in array)
                {
                    var tag = new VsTextAdornmentTag { myAttributeId = item };
                    Assert.True(_editAdapter.IsExternalEditTag(tag));
                }
            }

            [WpfFact]
            public void RightTagWithWrongAttributes()
            {
                Create("");
                var array = new[] { "dog", "cat" };
                foreach (var item in array)
                {
                    var tag = new VsTextAdornmentTag { myAttributeId = item };
                    Assert.False(_editAdapter.IsExternalEditTag(tag));
                }
            }
        }

        public sealed class MiscTest : ReSharperExternalEditAdapterTest
        {
            /// <summary>
            /// The Laszy property in imported collections can throw an exception due to composition
            /// failure.  Make sure this exception doesn't bring down the IsInterested check
            /// </summary>
            [WpfFact]
            public void Issue1381()
            {
                Create();
                var lazy = new Lazy<ITaggerProvider>(() => { throw new Exception(); });
                var list = new List<Lazy<ITaggerProvider>>
                {
                    lazy
                };
                _editAdapterRaw.TaggerProviders = list;
                var textView = CreateTextView();
                Assert.False(_editAdapter.IsInterested(textView, out ITagger<ITag> tagger));
            }
        }
    }
}
