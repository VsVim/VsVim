using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public sealed class SearchServiceTest : VimTestBase
    {
        private ITextBuffer _textBuffer;
        private ITextStructureNavigator _wordNavigator;
        private IVimGlobalSettings _globalSettings;
        private ITextSearchService _textSearch;
        private SearchService _searchRaw;
        private ISearchService _search;

        public void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
            _wordNavigator = WordUtilFactory.GetWordUtil(_textBuffer).CreateTextStructureNavigator(WordKind.NormalWord);
            _globalSettings = Vim.GlobalSettings;
            _globalSettings.Magic = true;
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = false;

            _textSearch = TextSearchService;
            _searchRaw = new SearchService(_textSearch, _globalSettings);
            _search = _searchRaw;
        }

        private FindOptions CreateFindOptions(string pattern, SearchKind kind, SearchOptions options)
        {
            var searchData = new SearchData(pattern, kind, options);
            var findData = _searchRaw.ConvertToFindData(searchData, _textBuffer.CurrentSnapshot, _wordNavigator);
            Assert.True(findData.IsSome());
            return findData.Value.FindOptions;
        }

        private SearchResult FindNextPattern(string pattern, Path path, SnapshotPoint point, int count)
        {
            var patternData = new PatternData(pattern, path);
            return _search.FindNextPattern(patternData, point, _wordNavigator, count);
        }

        [Fact]
        public void CreateFindOptions1()
        {
            Create("");
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.None);
            Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        [Fact]
        public void CreateFindOptions2()
        {
            Create("");
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.None);
            Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        [Fact]
        public void CreateFindOptions3()
        {
            Create("");
            var options = CreateFindOptions(@"\<sample\>", SearchKind.Forward, SearchOptions.None);
            Assert.Equal(FindOptions.WholeWord | FindOptions.MatchCase, options);
        }

        [Fact]
        public void CreateFindOptions4()
        {
            Create("");
            _globalSettings.IgnoreCase = false;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        [Fact]
        public void CreateFindOptions5()
        {
            Create("");
            _globalSettings.IgnoreCase = true;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            Assert.Equal(FindOptions.UseRegularExpressions, options);
        }

        [Fact]
        public void CreateFindOptions6()
        {
            Create("");
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = false;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.Equal(FindOptions.UseRegularExpressions, options);
        }

        [Fact]
        public void CreateFindOptions7()
        {
            Create("");
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = true;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.Equal(FindOptions.UseRegularExpressions, options);
        }

        [Fact]
        public void CreateFindOptions8()
        {
            Create("");
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = true;
            var options = CreateFindOptions("foo", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.Equal(FindOptions.UseRegularExpressions, options);
        }

        [Fact]
        public void CreateFindOptions9()
        {
            Create("");
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = true;
            var options = CreateFindOptions("fOo", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        [Fact]
        public void CreateFindOptions10()
        {
            Create("");
            var options = CreateFindOptions(PatternUtil.CreateWholeWord("sample"), SearchKind.Backward, SearchOptions.None);
            Assert.Equal(FindOptions.WholeWord | FindOptions.MatchCase | FindOptions.SearchReverse, options);
        }

        /// <summary>
        /// Make sure the conversion to FindOptions respects the case specifier over normal options
        /// </summary>
        [Fact]
        public void CreateFindOptions_RespectCaseSensitiveSpecifier()
        {
            Create("");
            _globalSettings.IgnoreCase = true;
            var options = CreateFindOptions(@"d\Cog", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            Assert.Equal(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        /// <summary>
        /// Make sure the conversion to FindOptions respects the case specifier over normal options
        /// </summary>
        [Fact]
        public void CreateFindOptions_RespectCaseInsensitiveSpecifier()
        {
            Create("");
            _globalSettings.IgnoreCase = false;
            var options = CreateFindOptions(@"d\cog", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            Assert.Equal(FindOptions.UseRegularExpressions, options);
        }

        /// <summary>
        /// Needs to respect the 'ignorecase' option if 'ConsiderIgnoreCase' is specified
        /// </summary>
        [Fact]
        public void FindNext_ConsiderIgnoreCase()
        {
            Create("cat dog FISH");
            _globalSettings.IgnoreCase = true;
            var data = VimUtil.CreateSearchData("fish", options: SearchOptions.ConsiderIgnoreCase);
            var result = _search.FindNext(data, _textBuffer.GetPoint(0), _wordNavigator);
            Assert.True(result.IsFound);
        }

        /// <summary>
        /// Respect the 'noignorecase' when 'ConsiderIgnoreCase' is specified
        /// </summary>
        [Fact]
        public void FindNext_IgnoreCaseConflictiong()
        {
            Create("cat dog FISH");
            _globalSettings.IgnoreCase = false;
            var data = VimUtil.CreateSearchData("fish", options: SearchOptions.ConsiderIgnoreCase);
            var result = _search.FindNext(data, _textBuffer.GetPoint(0), _wordNavigator);
            Assert.True(result.IsNotFound);
        }

        /// <summary>
        /// Verify it's actually doing a regular expression search when appropriate
        /// </summary>
        [Fact]
        public void FindNext_UseRegularExpression()
        {
            Create(@"cat bthe thedog");
            var data = VimUtil.CreateSearchData(@"\<the");
            var result = _search.FindNext(data, _textBuffer.GetPoint(0), _wordNavigator);
            Assert.Equal(9, result.AsFound().Item2.Start.Position);
        }

        /// <summary>
        /// Bad regular expressions can cause the FindNext API call to throw internally.  Make
        /// sure we wrap it and return a NotFound
        /// </summary>
        [Fact]
        public void FindNext_BadRegex()
        {
            Create("");
            var data = VimUtil.CreateSearchData("f(");
            var result = _search.FindNext(data, _textBuffer.GetPoint(0), _wordNavigator);
            Assert.True(result.IsNotFound);
        }

        /// <summary>
        /// Make sure we handle the 'nomagic' modifier
        /// </summary>
        [Fact]
        public void BadRegex_NoMagicSpecifierShouldBeHandled()
        {
            Create("");
            var searchData = new SearchData(@"\V", SearchKind.ForwardWithWrap, SearchOptions.None);
            var result = _search.FindNext(searchData, _textBuffer.GetPoint(0), _wordNavigator);
            Assert.True(result.IsNotFound);
        }

        /// <summary>
        /// Make sure we find the count occurrence of the item
        /// </summary>
        [Fact]
        public void FindNextMulitple_Count()
        {
            Create(" cat dog cat");
            var data = VimUtil.CreateSearchData("cat");
            var result = _search.FindNextMultiple(data, _textBuffer.GetPoint(0), _wordNavigator, 2);
            Assert.Equal(9, result.AsFound().Item2.Start.Position);
        }

        /// <summary>
        /// Make sure the count is taken into consideration
        /// </summary>
        [Fact]
        public void FindNextPattern_WithCount()
        {
            Create("cat dog cat", "cat");
            var result = FindNextPattern("cat", Path.Forward, _textBuffer.GetPoint(0), 2);
            Assert.True(result.IsFound);
            Assert.Equal(_textBuffer.GetLine(1).Extent, result.AsFound().Item2);
            Assert.False(result.AsFound().item3);
        }

        /// <summary>
        /// Don't make a partial match when using a whole word pattern
        /// </summary>
        [Fact]
        public void FindNextPattern_DontMatchPartialForWholeWord()
        {
            Create("dog doggy dog");
            var result = FindNextPattern(@"\<dog\>", Path.Forward, _textBuffer.GetPoint(0), 1);
            Assert.True(result.IsFound(10));
        }

        /// <summary>
        /// Do a backward search with 'wrapscan' enabled should go backwards
        /// </summary>
        [Fact]
        public void FindNextPattern_Backward()
        {
            Create("cat dog", "cat");
            _globalSettings.WrapScan = true;
            var result = FindNextPattern(@"\<cat\>", Path.Backward, _textBuffer.GetLine(1).Start, 1);
            Assert.True(result.IsFound(0));
        }

        /// <summary>
        /// Regression test for issue 398.  When starting on something other
        /// than the first character make sure we don't jump over an extra 
        /// word when searching for a whole word
        /// </summary>
        [Fact]
        public void FindNextPattern_StartOnSecondChar()
        {
            Create("cat cat cat");
            var result = FindNextPattern(@"\<cat\>", Path.Forward, _textBuffer.GetPoint(1), 1);
            Assert.True(result.IsFound(4));
        }

        /// <summary>
        /// Make sure that searching backward from the first char in a word doesn't
        /// count that word as an occurrence
        /// </summary>
        [Fact]
        public void FindNextPattern_BackwardFromFirstChar()
        {
            Create("cat cat cat");
            var result = FindNextPattern(@"cat", Path.Backward, _textBuffer.GetPoint(4), 1);
            Assert.True(result.IsFound(0));
        }

        /// <summary>
        /// Don't start the search on the current word start.  It should start afterwards
        /// so we don't match the current word
        /// </summary>
        [Fact]
        public void FindNextPattern_DontStartOnPointForward()
        {
            Create("foo bar", "foo");
            var result = FindNextPattern("foo", Path.Forward, _textBuffer.GetPoint(0), 1);
            Assert.Equal(_textBuffer.GetLine(1).Start, result.AsFound().Item2.Start);
        }

        /// <summary>
        /// Don't start the search on the current word start.  It should before the character
        /// when doing a backward search so we don't match the current word
        /// </summary>
        [Fact]
        public void FindNextPattern_DontStartOnPointBackward()
        {
            Create("foo bar", "foo");
            var result = FindNextPattern("foo", Path.Backward, _textBuffer.GetLine(1).Start, 1);
            Assert.Equal(_textBuffer.GetPoint(0), result.AsFound().Item2.Start);
        }

        /// <summary>
        /// Make sure that this takes into account the 'wrapscan' option going forward
        /// </summary>
        [Fact]
        public void FindNextPattern_ConsiderWrapScanForward()
        {
            Create("dog", "cat");
            _globalSettings.WrapScan = false;
            var result = FindNextPattern("dog", Path.Forward, _textBuffer.GetPoint(0), 1);
            Assert.True(result.IsNotFound);
            Assert.True(result.AsNotFound().Item2);
        }

        /// <summary>
        /// Make sure that this takes into account the 'wrapscan' option going forward
        /// </summary>
        [Fact]
        public void FindNextPattern_ConsiderWrapScanBackward()
        {
            Create("dog", "cat");
            _globalSettings.WrapScan = false;
            var result = FindNextPattern("dog", Path.Backward, _textBuffer.GetPoint(0), 1);
            Assert.True(result.IsNotFound);
            Assert.True(result.AsNotFound().Item2);
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
        [Fact]
        public void FindNextPattern_Regression_InfiniteLoop()
        {
            Create("i", "int foo()", "cat");
            var pattern = PatternUtil.CreateWholeWord("i");
            var result = FindNextPattern(pattern, Path.Backward, _textBuffer.GetPointInLine(2, 1), 1);
            Assert.True(result.IsFound(0));
        }
    }

}
