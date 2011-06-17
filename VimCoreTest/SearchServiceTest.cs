using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class SearchServiceTest
    {
        private ITextBuffer _textBuffer;
        private ITextStructureNavigator _navigator;
        private IVimGlobalSettings _globalSettings;
        private ITextSearchService _textSearch;
        private SearchService _searchRaw;
        private ISearchService _search;

        public void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateBuffer(lines);
            _navigator = VimUtil.CreateTextStructureNavigator(_textBuffer, WordKind.NormalWord);
            _globalSettings = new Vim.GlobalSettings();
            _globalSettings.Magic = true;
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = false;

            _textSearch = EditorUtil.FactoryService.TextSearchService;
            _searchRaw = new SearchService(_textSearch, _globalSettings);
            _search = _searchRaw;
        }

        private FindOptions CreateFindOptions(string pattern, SearchKind kind, SearchOptions options)
        {
            var searchData = new SearchData(pattern, kind, options);
            var findData = _searchRaw.ConvertToFindData(searchData, _textBuffer.CurrentSnapshot, _navigator);
            Assert.IsTrue(findData.IsSome());
            return findData.Value.FindOptions;
        }

        private SearchResult FindNextPattern(string pattern, Path path, SnapshotPoint point, int count)
        {
            var patternData = new PatternData(pattern, path);
            return _search.FindNextPattern(patternData, point, _navigator, count);
        }

        [Test]
        public void CreateFindOptions1()
        {
            Create("");
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions2()
        {
            Create("");
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions3()
        {
            Create("");
            var options = CreateFindOptions(@"\<sample\>", SearchKind.Forward, SearchOptions.None);
            Assert.AreEqual(FindOptions.WholeWord | FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions4()
        {
            Create("");
            _globalSettings.IgnoreCase = false;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions5()
        {
            Create("");
            _globalSettings.IgnoreCase = true;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
        }

        [Test]
        public void CreateFindOptions6()
        {
            Create("");
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = false;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
        }

        [Test]
        public void CreateFindOptions7()
        {
            Create("");
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = true;
            var options = CreateFindOptions("sample", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
        }

        [Test]
        public void CreateFindOptions8()
        {
            Create("");
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = true;
            var options = CreateFindOptions("foo", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions, options);
        }

        [Test]
        public void CreateFindOptions9()
        {
            Create("");
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = true;
            var options = CreateFindOptions("fOo", SearchKind.Forward, SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase);
            Assert.AreEqual(FindOptions.UseRegularExpressions | FindOptions.MatchCase, options);
        }

        [Test]
        public void CreateFindOptions10()
        {
            Create("");
            var options = CreateFindOptions(PatternUtil.CreateWholeWord("sample"), SearchKind.Backward, SearchOptions.None);
            Assert.AreEqual(FindOptions.WholeWord | FindOptions.MatchCase | FindOptions.SearchReverse, options);
        }

        /// <summary>
        /// Needs to respect the 'ignorecase' option if 'ConsiderIgnoreCase' is specified
        /// </summary>
        [Test]
        public void FindNext_ConsiderIgnoreCase()
        {
            Create("cat dog FISH");
            _globalSettings.IgnoreCase = true;
            var data = VimUtil.CreateSearchData("fish", options: SearchOptions.ConsiderIgnoreCase);
            var result = _search.FindNext(data, _textBuffer.GetPoint(0), _navigator);
            Assert.IsTrue(result.IsFound);
        }

        /// <summary>
        /// Respect the 'noignorecase' when 'ConsiderIgnoreCase' is specified
        /// </summary>
        [Test]
        public void FindNext_IgnoreCaseConflictiong()
        {
            Create("cat dog FISH");
            _globalSettings.IgnoreCase = false;
            var data = VimUtil.CreateSearchData("fish", options: SearchOptions.ConsiderIgnoreCase);
            var result = _search.FindNext(data, _textBuffer.GetPoint(0), _navigator);
            Assert.IsTrue(result.IsNotFound);
        }

        /// <summary>
        /// Verify it's actually doing a regular expression search when appropriate
        /// </summary>
        [Test]
        public void FindNext_UseRegularExpression()
        {
            Create(@"cat bthe thedog");
            var data = VimUtil.CreateSearchData(@"\<the");
            var result = _search.FindNext(data, _textBuffer.GetPoint(0), _navigator);
            Assert.AreEqual(9, result.AsFound().Item2.Start.Position);
        }

        /// <summary>
        /// Bad regular expressions can cause the FindNext API call to throw internally.  Make
        /// sure we wrap it and return a NotFound
        /// </summary>
        [Test]
        public void FindNext_BadRegex()
        {
            Create("");
            var data = VimUtil.CreateSearchData("f(");
            var result = _search.FindNext(data, _textBuffer.GetPoint(0), _navigator);
            Assert.IsTrue(result.IsNotFound);
        }

        /// <summary>
        /// Make sure we handle the 'nomagic' modifier
        /// </summary>
        [Test]
        public void BadRegex_NoMagicSpecifierShouldBeHandled()
        {
            Create("");
            var searchData = new SearchData(@"\V", SearchKind.ForwardWithWrap, SearchOptions.None);
            var result = _search.FindNext(searchData, _textBuffer.GetPoint(0), _navigator);
            Assert.IsTrue(result.IsNotFound);
        }

        /// <summary>
        /// Make sure we find the count occurrence of the item
        /// </summary>
        [Test]
        public void FindNextMulitple_Count()
        {
            Create(" cat dog cat");
            var data = VimUtil.CreateSearchData("cat");
            var result = _search.FindNextMultiple(data, _textBuffer.GetPoint(0), _navigator, 2);
            Assert.AreEqual(9, result.AsFound().Item2.Start.Position);
        }

        /// <summary>
        /// Make sure the count is taken into consideration
        /// </summary>
        [Test]
        public void FindNextPattern_WithCount()
        {
            Create("cat dog cat", "cat");
            var result = FindNextPattern("cat", Path.Forward, _textBuffer.GetPoint(0), 2);
            Assert.IsTrue(result.IsFound);
            Assert.AreEqual(_textBuffer.GetLine(1).Extent, result.AsFound().Item2);
            Assert.IsFalse(result.AsFound().item3);
        }

        /// <summary>
        /// Don't make a partial match when using a whole word pattern
        /// </summary>
        [Test]
        public void FindNextPattern_DontMatchPartialForWholeWord()
        {
            Create("dog doggy dog");
            var result = FindNextPattern(@"\<dog\>", Path.Forward, _textBuffer.GetPoint(0), 1);
            Assert.IsTrue(result.IsFound(10));
        }

        /// <summary>
        /// Do a backward search with 'wrapscan' enabled should go backwards
        /// </summary>
        [Test]
        public void FindNextPattern_Backward()
        {
            Create("cat dog", "cat");
            _globalSettings.WrapScan = true;
            var result = FindNextPattern(@"\<cat\>", Path.Backward, _textBuffer.GetLine(1).Start, 1);
            Assert.IsTrue(result.IsFound(0));
        }

        /// <summary>
        /// Regression test for issue 398.  When starting on something other
        /// than the first character make sure we don't jump over an extra 
        /// word when searching for a whole word
        /// </summary>
        [Test]
        public void FindNextPattern_StartOnSecondChar()
        {
            Create("cat cat cat");
            var result = FindNextPattern(@"\<cat\>", Path.Forward, _textBuffer.GetPoint(1), 1);
            Assert.IsTrue(result.IsFound(4));
        }

        /// <summary>
        /// Make sure that searching backward from the first char in a word doesn't
        /// count that word as an occurrence
        /// </summary>
        [Test]
        public void FindNextPattern_BackwardFromFirstChar()
        {
            Create("cat cat cat");
            var result = FindNextPattern(@"cat", Path.Backward, _textBuffer.GetPoint(4), 1);
            Assert.IsTrue(result.IsFound(0));
        }

        /// <summary>
        /// Don't start the search on the current word start.  It should start afterwards
        /// so we don't match the current word
        /// </summary>
        [Test]
        public void FindNextPattern_DontStartOnPointForward()
        {
            Create("foo bar", "foo");
            var result = FindNextPattern("foo", Path.Forward, _textBuffer.GetPoint(0), 1);
            Assert.AreEqual(_textBuffer.GetLine(1).Start, result.AsFound().Item2.Start);
        }

        /// <summary>
        /// Don't start the search on the current word start.  It should before the character
        /// when doing a backward search so we don't match the current word
        /// </summary>
        [Test]
        public void FindNextPattern_DontStartOnPointBackward()
        {
            Create("foo bar", "foo");
            var result = FindNextPattern("foo", Path.Backward, _textBuffer.GetLine(1).Start, 1);
            Assert.AreEqual(_textBuffer.GetPoint(0), result.AsFound().Item2.Start);
        }

        /// <summary>
        /// Make sure that this takes into account the 'wrapscan' option going forward
        /// </summary>
        [Test]
        public void FindNextPattern_ConsiderWrapScanForward()
        {
            Create("dog", "cat");
            _globalSettings.WrapScan = false;
            var result = FindNextPattern("dog", Path.Forward, _textBuffer.GetPoint(0), 1);
            Assert.IsTrue(result.IsNotFound);
            Assert.IsTrue(result.AsNotFound().Item2);
        }

        /// <summary>
        /// Make sure that this takes into account the 'wrapscan' option going forward
        /// </summary>
        [Test]
        public void FindNextPattern_ConsiderWrapScanBackward()
        {
            Create("dog", "cat");
            _globalSettings.WrapScan = false;
            var result = FindNextPattern("dog", Path.Backward, _textBuffer.GetPoint(0), 1);
            Assert.IsTrue(result.IsNotFound);
            Assert.IsTrue(result.AsNotFound().Item2);
        }
    }

}
