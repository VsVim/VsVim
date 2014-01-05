using System.Linq;
using EditorUtils;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class MotionCaptureTest : VimTestBase
    {
        private readonly IVimLocalSettings _localSettings;
        private readonly ITextView _textView;
        private readonly IIncrementalSearch _incrementalSearch;
        private readonly MotionCapture _captureRaw;
        private readonly IMotionCapture _capture;

        public MotionCaptureTest()
        {
            _textView = CreateTextView();

            var vimTextBuffer = Vim.CreateVimTextBuffer(_textView.TextBuffer);
            var vimBufferData = CreateVimBufferData(vimTextBuffer, _textView);
            _incrementalSearch = new IncrementalSearch(
                vimBufferData,
                CommonOperationsFactory.GetCommonOperations(vimBufferData));
            _localSettings = vimTextBuffer.LocalSettings;
            _captureRaw = new MotionCapture(
                vimBufferData,
                _incrementalSearch);
            _capture = _captureRaw;
        }

        internal BindResult<Motion> Process(string input, bool enter = false)
        {
            var res = _capture.GetMotionAndCount(KeyInputUtil.CharToKeyInput(input[0]));
            foreach (var cur in input.Skip(1))
            {
                Assert.True(res.IsNeedMoreInput);
                var needMore = res.AsNeedMoreInput();
                res = needMore.Item.BindFunction.Invoke(KeyInputUtil.CharToKeyInput(cur));
            }

            if (enter)
            {
                var needMore = res.AsNeedMoreInput();
                res = needMore.Item.BindFunction.Invoke(KeyInputUtil.EnterKey);
            }

            return res.Convert(x => x.Item1);
        }

        private void AssertMotion(string text, Motion motion)
        {
            var result = Process(text);
            Assert.True(result.IsComplete);
            Assert.Equal(motion, result.AsComplete().Item);
        }

        private void AssertMotion(KeyInput keyInput, Motion motion)
        {
            var result = _capture.GetMotionAndCount(keyInput);
            Assert.True(result.IsComplete);
            Assert.Equal(motion, result.AsComplete().Item.Item1);
        }

        private void AssertMotion(VimKey key, Motion motion)
        {
            AssertMotion(KeyInputUtil.VimKeyToKeyInput(key), motion);
        }

        private static MotionResult CreateMotionResult()
        {
            var point = MockObjectFactory.CreateSnapshotPoint(42);
            return VimUtil.CreateMotionResult(
                new SnapshotSpan(point, point),
                true,
                MotionKind.CharacterWiseInclusive);
        }

        private static FSharpOption<MotionResult> CreateMotionResultSome()
        {
            return FSharpOption.Create(CreateMotionResult());
        }

        [Fact]
        public void Word()
        {
            AssertMotion("w", Motion.NewWordForward(WordKind.NormalWord));
            AssertMotion("W", Motion.NewWordForward(WordKind.BigWord));
        }

        [Fact]
        public void BadInput()
        {
            var res = Process("z");
            Assert.True(res.IsError);
        }

        [Fact]
        public void EndOfLine()
        {
            AssertMotion("$", Motion.EndOfLine);
            AssertMotion(VimKey.End, Motion.EndOfLine);
        }

        [Fact]
        public void BeginingOfLine()
        {
            AssertMotion("0", Motion.BeginingOfLine);
        }

        [Fact]
        public void AllWord()
        {
            AssertMotion("aw", Motion.NewAllWord(WordKind.NormalWord));
            AssertMotion("aW", Motion.NewAllWord(WordKind.BigWord));
        }

        [Fact]
        public void LineFromTopOfWindow()
        {
            AssertMotion("H", Motion.LineFromTopOfVisibleWindow);
        }

        [Fact]
        public void CharLeft()
        {
            AssertMotion("h", Motion.CharLeft);
        }

        [Fact]
        public void CharRight()
        {
            AssertMotion("l", Motion.CharRight);
        }

        [Fact]
        public void SpaceLeft()
        {
            AssertMotion(VimKey.Back, Motion.SpaceLeft);
            AssertMotion(KeyNotationUtil.StringToKeyInput("<C-h>"), Motion.SpaceLeft);
        }

        [Fact]
        public void SpaceRight()
        {
            AssertMotion(" ", Motion.SpaceRight);
        }

        [Fact]
        public void ArrowLeft()
        {
            AssertMotion(VimKey.Left, Motion.ArrowLeft);
        }

        [Fact]
        public void ArrowRight()
        {
            AssertMotion(VimKey.Right, Motion.ArrowRight);
        }

        [Fact]
        public void LineUp()
        {
            AssertMotion("k", Motion.LineUp);
            AssertMotion(VimKey.Up, Motion.LineUp);
            AssertMotion(KeyNotationUtil.StringToKeyInput("<C-p>"), Motion.LineUp);
        }

        [Fact]
        public void EndOfWord()
        {
            AssertMotion("e", Motion.NewEndOfWord(WordKind.NormalWord));
            AssertMotion("E", Motion.NewEndOfWord(WordKind.BigWord));
        }

        [Fact]
        public void CharSearch_ToCharForward()
        {
            AssertMotion("fc", Motion.NewCharSearch(CharSearchKind.ToChar, Path.Forward, 'c'));
        }

        [Fact]
        public void CharSearch_TillCharForward()
        {
            AssertMotion("tc", Motion.NewCharSearch(CharSearchKind.TillChar, Path.Forward, 'c'));
        }

        [Fact]
        public void CharSearch_ToCharBackward()
        {
            AssertMotion("Fc", Motion.NewCharSearch(CharSearchKind.ToChar, Path.Backward, 'c'));
        }

        [Fact]
        public void CharSearch_TillCharBackward()
        {
            AssertMotion("Tc", Motion.NewCharSearch(CharSearchKind.TillChar, Path.Backward, 'c'));
        }

        [Fact]
        public void LineOrLastToFirstNonBlank()
        {
            AssertMotion("G", Motion.LineOrLastToFirstNonBlank);
        }

        [Fact]
        public void LineOrFirstToFirstNonBlank()
        {
            AssertMotion("gg", Motion.LineOrFirstToFirstNonBlank);
        }

        [Fact]
        public void LastNonBlankOnLine()
        {
            AssertMotion("g_", Motion.LastNonBlankOnLine);
        }

        [Fact]
        public void LineInMiddleOfVisibleWindow()
        {
            AssertMotion("M", Motion.LineInMiddleOfVisibleWindow);
        }

        [Fact]
        public void LineFromBottomOfVisibleWindow()
        {
            AssertMotion("L", Motion.LineFromBottomOfVisibleWindow);
        }

        [Fact]
        public void FirstNonBlankOnLine()
        {
            AssertMotion("_", Motion.FirstNonBlankOnLine);
        }

        [Fact]
        public void FirstNonBlankOnLineOnCurrentLine()
        {
            AssertMotion("^", Motion.FirstNonBlankOnCurrentLine);
        }

        [Fact]
        public void RepeatLastCharSearch()
        {
            AssertMotion(";", Motion.RepeatLastCharSearch);
            AssertMotion(",", Motion.RepeatLastCharSearchOpposite);
        }

        [Fact]
        public void SentenceForward()
        {
            AssertMotion(")", Motion.SentenceForward);
        }

        [Fact]
        public void SentenceBackward()
        {
            AssertMotion("(", Motion.SentenceBackward);
        }

        [Fact]
        public void AllSentence()
        {
            AssertMotion("as", Motion.AllSentence);
        }

        [Fact]
        public void ParagraphForward()
        {
            AssertMotion("}", Motion.ParagraphForward);
        }

        [Fact]
        public void ParagraphBackward()
        {
            AssertMotion("{", Motion.ParagraphBackward);
        }

        [Fact]
        public void SectionForwardOrOpenBrace()
        {
            AssertMotion("]]", Motion.SectionForward);
        }

        [Fact]
        public void SectionForwardOrCloseBrace()
        {
            AssertMotion("][", Motion.SectionForwardOrCloseBrace);
        }

        [Fact]
        public void SectionBackwardOrOpenBrace()
        {
            AssertMotion("[[", Motion.SectionBackwardOrOpenBrace);
        }

        [Fact]
        public void SectionBackwardOrCloseBrace()
        {
            AssertMotion("[]", Motion.SectionBackwardOrCloseBrace);
        }

        [Fact]
        public void ScreenColumn()
        {
            AssertMotion("|", Motion.ScreenColumn);
        }

        [Fact]
        public void QuotedString()
        {
            AssertMotion(@"a""", Motion.NewQuotedString('"'));
            AssertMotion("a'", Motion.NewQuotedString('\''));
            AssertMotion("a`", Motion.NewQuotedString('`'));
        }

        [Fact]
        public void QuotedStringContents1()
        {
            AssertMotion(@"i""", Motion.NewQuotedStringContents('"'));
            AssertMotion("i'", Motion.NewQuotedStringContents('\''));
            AssertMotion("i`", Motion.NewQuotedStringContents('`'));
        }

        [Fact]
        public void IncrementalSearch_Reverse()
        {
            _textView.TextBuffer.SetText("hello world");
            _textView.MoveCaretTo(_textView.GetEndPoint().Position);
            var motionResult = Process("?world", enter: true).AsComplete().Item;
            var searchData = ((Motion.Search)motionResult).Item;
            Assert.Equal("world", searchData.Pattern);
            Assert.Equal(Path.Backward, searchData.Path);
            Assert.True(searchData.Kind.IsBackwardWithWrap);
        }

        [Fact]
        public void IncrementalSearch_Forward()
        {
            _textView.SetText("hello world", caret: 0);
            var motionResult = Process("/world", enter: true).AsComplete().Item;
            var searchData = ((Motion.Search)motionResult).Item;
            Assert.Equal("world", searchData.Pattern);
            Assert.Equal(Path.Forward, searchData.Path);
            Assert.True(searchData.Kind.IsForwardWithWrap);
        }

        [Fact]
        public void IncrementalSearch_ForwardShouldRespectWrapScan()
        {
            _textView.SetText("cat dog");
            var didRun = false;
            _incrementalSearch.CurrentSearchUpdated += (_, args) =>
            {
                Assert.True(SearchKind.ForwardWithWrap == args.SearchResult.SearchData.Kind);
                didRun = true;
            };
            Process("/cat");
            Assert.True(didRun);
        }

        [Fact]
        public void IncrementalSearch_ForwardShouldRespectNoWrapScan()
        {
            _textView.SetText("cat dog");
            _localSettings.GlobalSettings.WrapScan = false;
            var didRun = false;
            _incrementalSearch.CurrentSearchUpdated += (_, args) =>
            {
                Assert.True(SearchKind.Forward == args.SearchResult.SearchData.Kind);
                didRun = true;
            };
            Process("/cat");
            Assert.True(didRun);
        }

        [Fact]
        public void IncrementalSearch_BackwardShouldRespectWrapScan()
        {
            _textView.SetText("cat dog");
            var didRun = false;
            _incrementalSearch.CurrentSearchUpdated += (_, args) =>
            {
                Assert.True(SearchKind.BackwardWithWrap == args.SearchResult.SearchData.Kind);
                didRun = true;
            };
            Process("?cat");
            Assert.True(didRun);
        }

        [Fact]
        public void IncrementalSearch_BackwardShouldRespectNoWrapScan()
        {
            _textView.SetText("cat dog");
            _localSettings.GlobalSettings.WrapScan = false;
            var didRun = false;
            _incrementalSearch.CurrentSearchUpdated += (_, args) =>
            {
                Assert.True(SearchKind.Backward == args.SearchResult.SearchData.Kind);
                didRun = true;
            };
            Process("?cat");
            Assert.True(didRun);
        }

        /// <summary>
        /// Incremental search input should be mapped via the command mapping.  Documentation
        /// specifies language mapping but implementation dictates command mapping
        /// </summary>
        [Fact]
        public void IncrementalSearch_ShouldUseCommandMapping()
        {
            _textView.SetText("cat dog");
            var result = _capture.GetMotionAndCount(KeyInputUtil.CharToKeyInput('/'));
            Assert.True(result.IsNeedMoreInput);
            Assert.Equal(result.AsNeedMoreInput().Item.KeyRemapMode, KeyRemapMode.Command);
        }

        /// <summary>
        /// Incremental search input should be mapped via the command mapping.  Documentation
        /// specifies language mapping but implementation dictates command mapping
        /// </summary>
        [Fact]
        public void IncrementalSearch_ShouldUseCommandMappingForAll()
        {
            _textView.SetText("cat dog");
            var result = _capture.GetMotionAndCount(KeyInputUtil.CharToKeyInput('/'));
            result = result.AsNeedMoreInput().Item.BindFunction.Invoke(KeyInputUtil.CharToKeyInput('a'));
            Assert.True(result.IsNeedMoreInput);
            Assert.Equal(result.AsNeedMoreInput().Item.KeyRemapMode, KeyRemapMode.Command);
        }

        [Fact]
        public void LineDownToFirstNonBlank_ShouldAcceptBothEnters()
        {
            _textView.SetText("cat\ndog\nbear");
            Assert.True(_capture.GetMotionAndCount(KeyInputUtil.EnterKey).IsComplete);
        }

        [Fact]
        public void LineDown()
        {
            AssertMotion(KeyNotationUtil.StringToKeyInput("<c-j>"), Motion.LineDown);
            AssertMotion(KeyNotationUtil.StringToKeyInput("<c-n>"), Motion.LineDown);
            AssertMotion("j", Motion.LineDown);
            AssertMotion(VimKey.Down, Motion.LineDown);
            AssertMotion(VimKey.LineFeed, Motion.LineDown);
        }

        [Fact]
        public void CommandMapSupportsAlternateKeys()
        {
            Assert.True(MapModule.TryFind(KeyInputSet.NewOneKeyInput(KeyInputUtil.EnterKey), _captureRaw.MotionBindingsMap).IsSome());
        }

        [Fact]
        public void Mark()
        {
            AssertMotion("`a", Motion.NewMark(LocalMark.OfChar('a').Value));
            AssertMotion("`b", Motion.NewMark(LocalMark.OfChar('b').Value));
        }

        [Fact]
        public void MarkLine()
        {
            AssertMotion("'a", Motion.NewMarkLine(LocalMark.OfChar('a').Value));
            AssertMotion("'b", Motion.NewMarkLine(LocalMark.OfChar('b').Value));
        }

        /// <summary>
        /// Make sure the bindings aren't incorrectly structured such that the incremental search
        /// begins on MotionCapture startup.  It should only begin during the processing of a motion
        /// </summary>
        [Fact]
        public void Search_EnsureIncrementalSearchNotStarted()
        {
            Assert.False(_incrementalSearch.InSearch);
        }

        /// <summary>
        /// Search should begin once the '/' is processed
        /// </summary>
        [Fact]
        public void Search_EnsureStartedOnSlash()
        {
            _capture.GetMotionAndCount('/');
            Assert.True(_incrementalSearch.InSearch);
        }

        /// <summary>
        /// Escape should end the search operation
        /// </summary>
        [Fact]
        public void Search_EscapeShouldEndTheSearch()
        {
            var result = _capture.GetMotionAndCount('/');
            Assert.True(result.IsNeedMoreInput);
            result.AsNeedMoreInput().Item.BindFunction.Invoke(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape));
            Assert.False(_incrementalSearch.InSearch);
        }

    }
}
