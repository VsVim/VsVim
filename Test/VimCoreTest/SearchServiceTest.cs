using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Xunit;
using Vim.Extensions;
using Moq;

namespace Vim.UnitTest
{
    public abstract class SearchServiceTest : VimTestBase
    {
        private ITextBuffer _textBuffer;
        private ITextStructureNavigator _wordNavigator;
        private IVimGlobalSettings _globalSettings;
        private ITextSearchService _textSearch;
        private SearchService _searchRaw;
        private ISearchService _search;

        internal virtual void Create(params string[] lines)
        {
            Create(TextSearchService, lines);
        }

        public virtual void Create(ITextSearchService textSearchService, params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
            _wordNavigator = (new WordUtil()).Snapshot.CreateTextStructureNavigator(WordKind.NormalWord, _textBuffer.ContentType);
            _globalSettings = Vim.GlobalSettings;
            _globalSettings.Magic = true;
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = false;

            _textSearch = textSearchService;
            _searchRaw = new SearchService(_textSearch, _globalSettings);
            _search = _searchRaw;
        }

        private FindOptions CreateFindOptions(string pattern, SearchKind kind, SearchOptions options)
        {
            var searchData = new SearchData(pattern, SearchOffsetData.None, kind, options);
            var serviceSearchData = _searchRaw.GetServiceSearchData(searchData, _wordNavigator);
            var findData = SearchService.ConvertToFindDataCore(serviceSearchData, _textBuffer.CurrentSnapshot);
            Assert.True(findData.IsResult);
            return findData.AsResult().FindOptions;
        }

        private SearchResult FindNextPattern(string pattern, SearchPath path, SnapshotPoint point, int count)
        {
            var searchData = new SearchData(pattern, path, _globalSettings.WrapScan);
            return _search.FindNextPattern(point, searchData, _wordNavigator, count);
        }

        public sealed class CreateFindOptionsTest : SearchServiceTest
        {
            [WpfFact]
            public void CreateFindOptions1()
            {
                Create("");
                var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.None);
                Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.MatchCase | FindOptions.Multiline, options);
            }

            [WpfFact]
            public void CreateFindOptions2()
            {
                Create("");
                var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.None);
                Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.MatchCase | FindOptions.Multiline, options);
            }

            [WpfFact]
            public void CreateFindOptions3()
            {
                Create("");
                var options = CreateFindOptions(@"\<sample\>", SearchKind.Forward, SearchOptions.None);
                Assert.Equal(FindOptions.WholeWord | FindOptions.MatchCase | FindOptions.Multiline, options);
            }

            [WpfFact]
            public void CreateFindOptions4()
            {
                Create("");
                _globalSettings.IgnoreCase = false;
                var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
                Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.MatchCase | FindOptions.Multiline, options);
            }

            [WpfFact]
            public void CreateFindOptions5()
            {
                Create("");
                _globalSettings.IgnoreCase = true;
                var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
                Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.Multiline, options);
            }

            [WpfFact]
            public void CreateFindOptions6()
            {
                Create("");
                _globalSettings.IgnoreCase = true;
                _globalSettings.SmartCase = false;
                var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
                Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.Multiline, options);
            }

            [WpfFact]
            public void CreateFindOptions7()
            {
                Create("");
                _globalSettings.IgnoreCase = true;
                _globalSettings.SmartCase = true;
                var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
                Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.Multiline, options);
            }

            [WpfFact]
            public void CreateFindOptions8()
            {
                Create("");
                _globalSettings.IgnoreCase = true;
                _globalSettings.SmartCase = true;
                var options = CreateFindOptions("foo", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
                Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.Multiline, options);
            }

            [WpfFact]
            public void CreateFindOptions9()
            {
                Create("");
                _globalSettings.IgnoreCase = true;
                _globalSettings.SmartCase = true;
                var options = CreateFindOptions("fOo", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
                Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.MatchCase | FindOptions.Multiline, options);
            }

            [WpfFact]
            public void CreateFindOptions10()
            {
                Create("");
                var options = CreateFindOptions(PatternUtil.CreateWholeWord("sample"), SearchKind.Backward, SearchOptions.None);
                Assert.Equal(FindOptions.WholeWord | FindOptions.MatchCase | FindOptions.SearchReverse | FindOptions.Multiline, options);
            }

            /// <summary>
            /// Make sure the conversion to FindOptions respects the case specifier over normal options
            /// </summary>
            [WpfFact]
            public void RespectCaseSensitiveSpecifier()
            {
                Create("");
                _globalSettings.IgnoreCase = true;
                var options = CreateFindOptions(@"d\Cog", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
                Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.MatchCase | FindOptions.Multiline, options);
            }

            /// <summary>
            /// Make sure the conversion to FindOptions respects the case specifier over normal options
            /// </summary>
            [WpfFact]
            public void RespectCaseInsensitiveSpecifier()
            {
                Create("");
                _globalSettings.IgnoreCase = false;
                var options = CreateFindOptions(@"d\cog", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
                Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.Multiline, options);
            }
        }

        public sealed class FindNextTest : SearchServiceTest
        {
            /// <summary>
            /// Needs to respect the 'ignorecase' option if 'ConsiderIgnoreCase' is specified
            /// </summary>
            [WpfFact]
            public void ConsiderIgnoreCase()
            {
                Create("cat dog FISH");
                _globalSettings.IgnoreCase = true;
                var data = VimUtil.CreateSearchData("fish", options: SearchOptions.ConsiderIgnoreCase);
                var result = _search.FindNext(_textBuffer.GetPoint(0), data, _wordNavigator);
                Assert.True(result.IsFound);
            }

            /// <summary>
            /// Respect the 'noignorecase' when 'ConsiderIgnoreCase' is specified
            /// </summary>
            [WpfFact]
            public void IgnoreCaseConflictiong()
            {
                Create("cat dog FISH");
                _globalSettings.IgnoreCase = false;
                var data = VimUtil.CreateSearchData("fish", options: SearchOptions.ConsiderIgnoreCase);
                var result = _search.FindNext(_textBuffer.GetPoint(0), data, _wordNavigator);
                Assert.True(result.IsNotFound);
            }

            /// <summary>
            /// Verify it's actually doing a regular expression search when appropriate
            /// </summary>
            [WpfFact]
            public void UseRegularExpression()
            {
                Create(@"cat bthe thedog");
                var data = VimUtil.CreateSearchData(@"\<the");
                var result = _search.FindNext(_textBuffer.GetPoint(0), data, _wordNavigator);
                Assert.Equal(9, result.AsFound().SpanWithOffset.Start.Position);
            }

            /// <summary>
            /// Bad regular expressions can cause the FindNext API call to throw internally.  Make
            /// sure we wrap it and return a NotFound
            /// </summary>
            [WpfFact]
            public void BadRegex()
            {
                Create("");
                var data = VimUtil.CreateSearchData("f(");
                var result = _search.FindNext(_textBuffer.GetPoint(0), data, _wordNavigator);
                Assert.True(result.IsNotFound);
            }

            /// <summary>
            /// Make sure we handle the 'nomagic' modifier
            /// </summary>
            [WpfFact]
            public void BadRegex_NoMagicSpecifierShouldBeHandled()
            {
                Create("");
                var searchData = new SearchData(@"\Va", SearchOffsetData.None, SearchKind.ForwardWithWrap, SearchOptions.None);
                var result = _search.FindNext(_textBuffer.GetPoint(0), searchData, _wordNavigator);
                Assert.True(result.IsNotFound);
            }

            /// <summary>
            /// Make sure we find the count occurrence of the item
            /// </summary>
            [WpfFact]
            public void FindNextPattern_Count()
            {
                Create(" cat dog cat");
                var data = VimUtil.CreateSearchData("cat");
                var result = _search.FindNextPattern(_textBuffer.GetPoint(0), data, _wordNavigator, 2);
                Assert.Equal(9, result.AsFound().SpanWithOffset.Start.Position);
            }
        }

        public sealed class FindNextPatternTest : SearchServiceTest
        {
            /// <summary>
            /// Make sure the count is taken into consideration
            /// </summary>
            [WpfFact]
            public void WithCount()
            {
                Create("cat dog cat", "cat");
                var result = FindNextPattern("cat", SearchPath.Forward, _textBuffer.GetPoint(0), 2);
                Assert.True(result.IsFound);
                Assert.Equal(_textBuffer.GetLine(1).Extent, result.AsFound().SpanWithOffset);
                Assert.False(result.AsFound().DidWrap);
            }

            /// <summary>
            /// Don't make a partial match when using a whole word pattern
            /// </summary>
            [WpfFact]
            public void DontMatchPartialForWholeWord()
            {
                Create("dog doggy dog");
                var result = FindNextPattern(@"\<dog\>", SearchPath.Forward, _textBuffer.GetPoint(0), 1);
                Assert.True(result.IsFound(10));
            }

            /// <summary>
            /// Do a backward search with 'wrapscan' enabled should go backwards
            /// </summary>
            [WpfFact]
            public void Backward()
            {
                Create("cat dog", "cat");
                _globalSettings.WrapScan = true;
                var result = FindNextPattern(@"\<cat\>", SearchPath.Backward, _textBuffer.GetLine(1).Start, 1);
                Assert.True(result.IsFound(0));
            }

            /// <summary>
            /// Regression test for issue 398.  When starting on something other
            /// than the first character make sure we don't jump over an extra 
            /// word when searching for a whole word
            /// </summary>
            [WpfFact]
            public void StartOnSecondChar()
            {
                Create("cat cat cat");
                var result = FindNextPattern(@"\<cat\>", SearchPath.Forward, _textBuffer.GetPoint(1), 1);
                Assert.True(result.IsFound(4));
            }

            /// <summary>
            /// Make sure that searching backward from the first char in a word doesn't
            /// count that word as an occurrence
            /// </summary>
            [WpfFact]
            public void BackwardFromFirstChar()
            {
                Create("cat cat cat");
                var result = FindNextPattern(@"cat", SearchPath.Backward, _textBuffer.GetPoint(4), 1);
                Assert.True(result.IsFound(0));
            }

            /// <summary>
            /// Don't start the search on the current word start.  It should start afterwards
            /// so we don't match the current word
            /// </summary>
            [WpfFact]
            public void DontStartOnPointForward()
            {
                Create("foo bar", "foo");
                var result = FindNextPattern("foo", SearchPath.Forward, _textBuffer.GetPoint(0), 1);
                Assert.Equal(_textBuffer.GetLine(1).Start, result.AsFound().SpanWithOffset.Start);
            }

            /// <summary>
            /// Don't start the search on the current word start.  It should before the character
            /// when doing a backward search so we don't match the current word
            /// </summary>
            [WpfFact]
            public void DontStartOnPointBackward()
            {
                Create("foo bar", "foo");
                var result = FindNextPattern("foo", SearchPath.Backward, _textBuffer.GetLine(1).Start, 1);
                Assert.Equal(_textBuffer.GetPoint(0), result.AsFound().SpanWithOffset.Start);
            }

            /// <summary>
            /// Make sure that this takes into account the 'wrapscan' option going forward
            /// </summary>
            [WpfFact]
            public void ConsiderWrapScanForward()
            {
                Create("dog", "cat");
                _globalSettings.WrapScan = false;
                var result = FindNextPattern("dog", SearchPath.Forward, _textBuffer.GetPoint(0), 1);
                Assert.True(result.IsNotFound);
                Assert.True(result.AsNotFound().CanFindWithWrap);
            }

            /// <summary>
            /// Make sure that this takes into account the 'wrapscan' option going forward
            /// </summary>
            [WpfFact]
            public void ConsiderWrapScanBackward()
            {
                Create("dog", "cat");
                _globalSettings.WrapScan = false;
                var result = FindNextPattern("dog", SearchPath.Backward, _textBuffer.GetPoint(0), 1);
                Assert.True(result.IsNotFound);
                Assert.True(result.AsNotFound().CanFindWithWrap);
            }

            /// <summary>
            /// There is a bug in the Vs 2010 implementation of the ITextSearchService which causes
            /// it to enter an infinite loop if the following conditions are met.  
            ///
            ///   1. Search is for a whole word
            ///   2. Search is backwards
            ///   3. Search string is 1 or 2 characters long
            ///   4. Any line above the search point starts with the search string but doesn't match
            ///      it's contents
            ///
            /// This is very impactful to C++ projects where 'i' is a common variable and lines at
            /// the top of the file commonly start with 'i' as they have a return type of 'int'.  
            /// 
            /// Our implementation of ITextSearchService works around this issue.  Make sure we don't
            /// regress the behavior
            /// </summary>
            [WpfFact]
            public void Regression_InfiniteLoop()
            {
                Create("i", "int foo()", "cat");
                var pattern = PatternUtil.CreateWholeWord("i");
                var result = FindNextPattern(pattern, SearchPath.Backward, _textBuffer.GetPointInLine(2, 1), 1);
                Assert.True(result.IsFound(0));
            }
        }

        public abstract class ApplySearchOffsetDataTest : SearchServiceTest
        {
            private Span ApplySearchOffsetData(SnapshotSpan span, SearchOffsetData searchOffsetData)
            {
                var searchData = new SearchData("", searchOffsetData, SearchKind.Forward, SearchOptions.None);
                var serviceSearchData = _searchRaw.GetServiceSearchData(searchData, _wordNavigator);
                return _searchRaw.ApplySearchOffsetData(serviceSearchData, span).Value;
            }

            public sealed class LineTest : ApplySearchOffsetDataTest
            {
                [WpfFact]
                public void OneLineDown()
                {
                    Create("cat", "dog", "fish");
                    var span = ApplySearchOffsetData(_textBuffer.GetLineSpan(0, 2), SearchOffsetData.NewLine(1));
                    Assert.Equal(_textBuffer.GetLineSpan(1, 1), span);
                }

                [WpfFact]
                public void TooManyLinesDown()
                {
                    Create("cat", "dog", "fish");
                    var span = ApplySearchOffsetData(_textBuffer.GetLineSpan(0, 2), SearchOffsetData.NewLine(100));
                    Assert.Equal(_textBuffer.GetLineSpan(2, 1), span);
                }

                [WpfFact]
                public void OneLineAbove()
                {
                    Create("cat", "dog", "fish");
                    var span = ApplySearchOffsetData(_textBuffer.GetLineSpan(1, 2), SearchOffsetData.NewLine(-1));
                    Assert.Equal(_textBuffer.GetLineSpan(0, 1), span);
                }
            }

            public sealed class EndTest : ApplySearchOffsetDataTest
            {
                [WpfFact]
                public void ZeroCase()
                {
                    Create("cat", "dog", "fish");
                    var span = ApplySearchOffsetData(_textBuffer.GetLineSpan(0, 1, 2), SearchOffsetData.NewEnd(0));
                    Assert.Equal(_textBuffer.GetLineSpan(0, 2, 1), span);
                }

                [WpfFact]
                public void After()
                {
                    Create("cat", "dog", "fish");
                    var span = ApplySearchOffsetData(_textBuffer.GetLineSpan(0, 1, 1), SearchOffsetData.NewEnd(1));
                    Assert.Equal(_textBuffer.GetLineSpan(0, 2, 1), span);
                }

                [WpfFact]
                public void AfterAcrossLines()
                {
                    Create("cat", "dog", "fish");
                    var span = ApplySearchOffsetData(_textBuffer.GetLineSpan(0, 2), SearchOffsetData.NewEnd(2));
                    Assert.Equal(_textBuffer.GetLineSpan(1, 1), span);
                }

                [WpfFact]
                public void Before()
                {
                    Create("cat", "dog", "fish");
                    var span = ApplySearchOffsetData(_textBuffer.GetLineSpan(0, 2), SearchOffsetData.NewEnd(-1));
                    Assert.Equal(_textBuffer.GetLineSpan(0, 1), span);
                }
            }

            public sealed class SearchTest : ApplySearchOffsetDataTest
            {
                [WpfFact]
                public void SimpleCase()
                {
                    Create("big", "cat", "dog", "fish");
                    var span = ApplySearchOffsetData(_textBuffer.GetLineSpan(0, 2), SearchOffsetData.NewSearch(new PatternData("dog", SearchPath.Forward)));
                    Assert.Equal(_textBuffer.GetLineSpan(2, 3), span);
                }
            }
        }

        public sealed class CacheTest : SearchServiceTest
        {
            private Mock<ITextSearchService> _mock;
            private int _searchCount = 0;

            public CacheTest()
            {
                _mock = new Mock<ITextSearchService>();
                _mock
                    .Setup(x => x.FindNext(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<FindData>()))
                    .Returns<int, bool, FindData>((position, wrapAround, findData) =>
                        {
                            _searchCount++;
                            return TextSearchService.FindNext(position, wrapAround, findData);
                        });
            }

            internal override void Create(params string[] lines)
            {
                base.Create(_mock.Object, lines);
            }

            private void FindNext(string text, int position = 0, SearchPath path = null, bool isWrap = true, ITextStructureNavigator navigator = null)
            {
                navigator = navigator ?? _wordNavigator;
                path = path ?? SearchPath.Forward;
                var point = _textBuffer.GetPoint(position);
                _search.FindNext(point, new SearchData(text, path, isWrap), navigator);
            }

            [WpfFact]
            public void SameText()
            {
                Create("cat dog");
                for (var i = 0; i < 10; i++)
                {
                    FindNext("dog");
                    Assert.Equal(2, _searchCount);
                }
            }

            [WpfFact]
            public void DifferentTextSameSnapshot()
            {
                Create("big cat dog");
                FindNext("dog");
                Assert.Equal(2, _searchCount);
                FindNext("cat");
                Assert.Equal(4, _searchCount);
                FindNext("dog");
                Assert.Equal(4, _searchCount);
            }

            [WpfFact]
            public void SameTextDifferentSnapshot()
            {
                Create("cat dog");
                FindNext("dog");
                Assert.Equal(2, _searchCount);
                _textBuffer.Replace(new Span(0, 0), "foo ");
                FindNext("dog");
                Assert.Equal(4, _searchCount);
            }

            [WpfFact]
            public void SameTextDifferentStartPoint()
            {
                Create("cat dog");
                FindNext("dog", position: 0);
                FindNext("dog", position: 1);
                Assert.Equal(4, _searchCount);
            }
        }
    }
}
