using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Vim;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public abstract class ClassifierTest : VimTestBase
    {
        public sealed class BasicTest : ClassifierTest
        {
            private readonly ITextBuffer _textBuffer;
            private readonly TextBasicTaggerSource<IClassificationTag> _source;
            private readonly IClassifier _classifier;

            public BasicTest()
            {
                _textBuffer = CreateTextBuffer();

                var classificationType = VimEditorHost.ClassificationTypeRegistryService.GetOrCreateClassificationType("classifier test");
                _source = new TextBasicTaggerSource<IClassificationTag>(new ClassificationTag(classificationType));
                _classifier = TaggerUtil.CreateBasicClassifierRaw(_source);
            }

            [WpfFact]
            public void SimpleGet()
            {
                _source.Text = "cat";
                _textBuffer.SetText("cat a cat");
                var list = _classifier.GetClassificationSpans(_textBuffer.GetExtent());
                Assert.Equal(2, list.Count);
                Assert.Equal(
                    new [] { new Span(0, 3), new Span(6, 3) },
                    list.Select(x => x.Span.Span));
            }

            [WpfFact]
            public void ChangeEvent()
            {
                int count = 0;
                _source.Text = "dog";
                _source.Changed += delegate { count++; };
                _source.Text = "bar";
                Assert.Equal(1, count);
                _source.Text = "bar";
                Assert.Equal(1, count);
            }
        }

        public sealed class AsyncTest : ClassifierTest
        {
            private readonly ITextBuffer _textBuffer;
            private readonly TextAsyncTaggerSource<IClassificationTag> _source;
            private readonly AsyncTagger<Tuple<string, IClassificationTag>, IClassificationTag> _asyncTagger;
            private readonly IClassifier _classifier;

            public AsyncTest()
            {
                _textBuffer = CreateTextBuffer();

                var classificationType = VimEditorHost.ClassificationTypeRegistryService.GetOrCreateClassificationType("classifier test");
                _source = new TextAsyncTaggerSource<IClassificationTag>(new ClassificationTag(classificationType), _textBuffer);
                _asyncTagger = new AsyncTagger<Tuple<string, IClassificationTag>, IClassificationTag>(_source);
                _classifier = new Classifier(_asyncTagger);
            }

            IList<ClassificationSpan> GetClassificationSpansFull(SnapshotSpan span, TestableSynchronizationContext context)
            {
                _classifier.GetClassificationSpans(span);
                _asyncTagger.WaitForBackgroundToComplete(context);
                return _classifier.GetClassificationSpans(span);
            }

            [WpfFact]
            public void SimpleGet()
            {
                using (var context = new TestableSynchronizationContext())
                {
                    _source.Text = "cat";
                    _textBuffer.SetText("cat a cat");

                    var list = GetClassificationSpansFull(_textBuffer.GetExtent(), context);
                    Assert.Equal(2, list.Count);
                    Assert.Equal(
                        new[] { new Span(0, 3), new Span(6, 3) },
                        list.Select(x => x.Span.Span));
                }
            }

            [WpfFact]
            public void ChangeFromComplete()
            {
                using (var context = new TestableSynchronizationContext())
                {
                    _source.Text = "cat";
                    _textBuffer.SetText("cat a cat");

                    _classifier.GetClassificationSpans(_textBuffer.GetExtent());
                    var count = 0;
                    _classifier.ClassificationChanged += delegate { count++; };
                    _asyncTagger.WaitForBackgroundToComplete(context);
                    Assert.Equal(1, count);
                }
            }
        }
    }
}
