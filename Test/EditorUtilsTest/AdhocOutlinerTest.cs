using EditorUtils.Implementation.Utilities;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace EditorUtils.UnitTest
{
    public sealed class AdhocOutlinerTest : EditorHostTest
    {
        private readonly ITextBuffer _textBuffer;
        private readonly AdhocOutliner _outlinerRaw;
        private readonly IAdhocOutliner _outliner;

        public AdhocOutlinerTest()
        {
            _textBuffer = CreateTextBuffer();
            _outlinerRaw = new AdhocOutliner(_textBuffer);
            _outliner = _outlinerRaw;
            _textBuffer.Properties.AddProperty(AdhocOutliner.OutlinerTaggerKey, null);
        }

        [Fact]
        public void CreateDeleteSequence()
        {
            _textBuffer.SetText("hello world");
            var region = _outliner.CreateOutliningRegion(_textBuffer.GetExtent(), SpanTrackingMode.EdgeExclusive, "", "");
            Assert.Equal(1, _outliner.GetOutliningRegions(_textBuffer.GetExtent()).Count);
            _outliner.DeleteOutliningRegion(region.Cookie);
            Assert.Equal(0, _outliner.GetOutliningRegions(_textBuffer.GetExtent()).Count);
        }

        [Fact]
        public void Properties()
        {
            _textBuffer.SetText("hello world");
            var region = _outliner.CreateOutliningRegion(_textBuffer.GetSpan(0, 1), SpanTrackingMode.EdgeExclusive, "text", "hint");
            Assert.Equal("text", region.Tag.CollapsedForm);
            Assert.Equal("hint", region.Tag.CollapsedHintForm);
        }

        [Fact]
        public void CheckTagger()
        {
            _textBuffer.Properties.RemoveProperty(AdhocOutliner.OutlinerTaggerKey);
            Assert.Throws<Exception>(() => _outliner.GetOutliningRegions(_textBuffer.GetExtent()));
        }
    }
}
