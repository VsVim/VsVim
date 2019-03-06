using System.Linq;
using Vim.EditorHost;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Xunit;
using System;

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
                res = needMore.BindData.BindFunction.Invoke(KeyInputUtil.CharToKeyInput(cur));
            }

            if (enter)
            {
                var needMore = res.AsNeedMoreInput();
                res = needMore.BindData.BindFunction.Invoke(KeyInputUtil.EnterKey);
            }

            return res.Convert(x => x.Item1);
        }

        private void AssertMotion(string text, Motion motion)
        {
            var result = Process(text);
            Assert.True(result.IsComplete);
            Assert.Equal(motion, result.AsComplete().Result);
        }

        private void AssertMotion(KeyInput keyInput, Motion motion)
        {
            var result = _capture.GetMotionAndCount(keyInput);
            Assert.True(result.IsComplete);
            Assert.Equal(motion, result.AsComplete().Result.Item1);
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

        [WpfFact]
        public void Word()
        {
            AssertMotion("w", Motion.NewWordForward(WordKind.NormalWord));
            AssertMotion("W", Motion.NewWordForward(WordKind.BigWord));
        }

        [WpfFact]
        public void BadInput()
        {
            var res = Process("z");
            Assert.True(res.IsError);
        }

        [WpfFact]
        public void EndOfLine()
        {
            AssertMotion("$", Motion.EndOfLine);
            AssertMotion(VimKey.End, Motion.EndOfLine);
        }

        [WpfFact]
        public void BeginingOfLine()
        {
            AssertMotion("0", Motion.BeginingOfLine);
        }

        [WpfFact]
        public void AllWord()
        {
            AssertMotion("aw", Motion.NewAllWord(WordKind.NormalWord));
            AssertMotion("aW", Motion.NewAllWord(WordKind.BigWord));
        }

        [WpfFact]
        public void LineFromTopOfWindow()
        {
            AssertMotion("H", Motion.LineFromTopOfVisibleWindow);
        }

        [WpfFact]
        public void CharLeft()
        {
            AssertMotion("h", Motion.CharLeft);
        }

        [WpfFact]
        public void CharRight()
        {
            AssertMotion("l", Motion.CharRight);
        }

        [WpfFact]
        public void SpaceLeft()
        {
            AssertMotion(VimKey.Back, Motion.SpaceLeft);
            AssertMotion(KeyNotationUtil.StringToKeyInput("<C-h>"), Motion.SpaceLeft);
        }

        [WpfFact]
        public void SpaceRight()
        {
            AssertMotion(" ", Motion.SpaceRight);
        }

        [WpfFact]
        public void ArrowLeft()
        {
            AssertMotion(VimKey.Left, Motion.ArrowLeft);
        }

        [WpfFact]
        public void ArrowRight()
        {
            AssertMotion(VimKey.Right, Motion.ArrowRight);
        }

        [WpfFact]
        public void LineUp()
        {
            AssertMotion("k", Motion.LineUp);
            AssertMotion(VimKey.Up, Motion.LineUp);
            AssertMotion(KeyNotationUtil.StringToKeyInput("<C-p>"), Motion.LineUp);
        }

        [WpfFact]
        public void EndOfWord()
        {
            AssertMotion("e", Motion.NewEndOfWord(WordKind.NormalWord));
            AssertMotion("E", Motion.NewEndOfWord(WordKind.BigWord));
        }

        [WpfFact]
        public void CharSearch_ToCharForward()
        {
            AssertMotion("fc", Motion.NewCharSearch(CharSearchKind.ToChar, SearchPath.Forward, 'c'));
        }

        [WpfFact]
        public void CharSearch_TillCharForward()
        {
            AssertMotion("tc", Motion.NewCharSearch(CharSearchKind.TillChar, SearchPath.Forward, 'c'));
        }

        [WpfFact]
        public void CharSearch_ToCharBackward()
        {
            AssertMotion("Fc", Motion.NewCharSearch(CharSearchKind.ToChar, SearchPath.Backward, 'c'));
        }

        [WpfFact]
        public void CharSearch_TillCharBackward()
        {
            AssertMotion("Tc", Motion.NewCharSearch(CharSearchKind.TillChar, SearchPath.Backward, 'c'));
        }

        [WpfFact]
        public void LineOrLastToFirstNonBlank()
        {
            AssertMotion("G", Motion.LineOrLastToFirstNonBlank);
        }

        [WpfFact]
        public void LineOrFirstToFirstNonBlank()
        {
            AssertMotion("gg", Motion.LineOrFirstToFirstNonBlank);
        }

        [WpfFact]
        public void LastNonBlankOnLine()
        {
            AssertMotion("g_", Motion.LastNonBlankOnLine);
        }

        [WpfFact]
        public void LineInMiddleOfVisibleWindow()
        {
            AssertMotion("M", Motion.LineInMiddleOfVisibleWindow);
        }

        [WpfFact]
        public void LineFromBottomOfVisibleWindow()
        {
            AssertMotion("L", Motion.LineFromBottomOfVisibleWindow);
        }

        [WpfFact]
        public void FirstNonBlankOnLine()
        {
            AssertMotion("_", Motion.FirstNonBlankOnLine);
        }

        [WpfFact]
        public void FirstNonBlankOnLineOnCurrentLine()
        {
            AssertMotion("^", Motion.FirstNonBlankOnCurrentLine);
        }

        [WpfFact]
        public void RepeatLastCharSearch()
        {
            AssertMotion(";", Motion.RepeatLastCharSearch);
            AssertMotion(",", Motion.RepeatLastCharSearchOpposite);
        }

        [WpfFact]
        public void SentenceForward()
        {
            AssertMotion(")", Motion.SentenceForward);
        }

        [WpfFact]
        public void SentenceBackward()
        {
            AssertMotion("(", Motion.SentenceBackward);
        }

        [WpfFact]
        public void AllSentence()
        {
            AssertMotion("as", Motion.AllSentence);
        }

        [WpfFact]
        public void AllParagraph()
        {
            AssertMotion("ap", Motion.AllParagraph);
        }

        [WpfFact]
        public void InnerParagraph()
        {
            AssertMotion("ip", Motion.InnerParagraph);
        }

        [WpfFact]
        public void ParagraphForward()
        {
            AssertMotion("}", Motion.ParagraphForward);
        }

        [WpfFact]
        public void ParagraphBackward()
        {
            AssertMotion("{", Motion.ParagraphBackward);
        }

        [WpfFact]
        public void SectionForwardOrOpenBrace()
        {
            AssertMotion("]]", Motion.SectionForward);
        }

        [WpfFact]
        public void SectionForwardOrCloseBrace()
        {
            AssertMotion("][", Motion.SectionForwardOrCloseBrace);
        }

        [WpfFact]
        public void SectionBackwardOrOpenBrace()
        {
            AssertMotion("[[", Motion.SectionBackwardOrOpenBrace);
        }

        [WpfFact]
        public void SectionBackwardOrCloseBrace()
        {
            AssertMotion("[]", Motion.SectionBackwardOrCloseBrace);
        }

        [WpfFact]
        public void ScreenColumn()
        {
            AssertMotion("|", Motion.ScreenColumn);
        }

        [WpfFact]
        public void NextMatch_Forward()
        {
            AssertMotion("gn", Motion.NewNextMatch(SearchPath.Forward));
        }

        [WpfFact]
        public void NextMatch_Backward()
        {
            AssertMotion("gN", Motion.NewNextMatch(SearchPath.Backward));
        }

        [WpfFact]
        public void MoveCaretToMouse()
        {
            AssertMotion(VimKey.LeftMouse, Motion.MoveCaretToMouse);
        }

        [WpfFact]
        public void QuotedString()
        {
            AssertMotion(@"a""", Motion.NewQuotedString('"'));
            AssertMotion("a'", Motion.NewQuotedString('\''));
            AssertMotion("a`", Motion.NewQuotedString('`'));
        }

        [WpfFact]
        public void QuotedStringContents1()
        {
            AssertMotion(@"i""", Motion.NewQuotedStringContents('"'));
            AssertMotion("i'", Motion.NewQuotedStringContents('\''));
            AssertMotion("i`", Motion.NewQuotedStringContents('`'));
        }

        [WpfFact]
        public void IncrementalSearch_Reverse()
        {
            _textView.TextBuffer.SetText("hello world");
            _textView.MoveCaretTo(_textView.GetEndPoint().Position);
            var motionResult = Process("?world", enter: true).AsComplete().Result;
            var searchData = ((Motion.Search)motionResult).SearchData;
            Assert.Equal("world", searchData.Pattern);
            Assert.Equal(SearchPath.Backward, searchData.Path);
            Assert.True(searchData.Kind.IsBackwardWithWrap);
        }

        [WpfFact]
        public void IncrementalSearch_Forward()
        {
            _textView.SetText("hello world", caret: 0);
            var motionResult = Process("/world", enter: true).AsComplete().Result;
            var searchData = ((Motion.Search)motionResult).SearchData;
            Assert.Equal("world", searchData.Pattern);
            Assert.Equal(SearchPath.Forward, searchData.Path);
            Assert.True(searchData.Kind.IsForwardWithWrap);
        }

        [WpfFact]
        public void IncrementalSearch_ForwardShouldRespectWrapScan()
        {
            _textView.SetText("cat dog");
            var didRun = false;
            _incrementalSearch.OnSearchEnd(searchResult =>
            {
                Assert.True(SearchKind.ForwardWithWrap == searchResult.SearchData.Kind);
                didRun = true;
            });
            Process("/cat");
            Assert.True(didRun);
        }

        [WpfFact]
        public void IncrementalSearch_ForwardShouldRespectNoWrapScan()
        {
            _textView.SetText("cat dog");
            _localSettings.GlobalSettings.WrapScan = false;
            var didRun = false;
            _incrementalSearch.OnSearchEnd(searchResult =>
            {
                Assert.True(SearchKind.Forward == searchResult.SearchData.Kind);
                didRun = true;
            });
            Process("/cat");
            Assert.True(didRun);
        }

        [WpfFact]
        public void IncrementalSearch_BackwardShouldRespectWrapScan()
        {
            _textView.SetText("cat dog");
            var didRun = false;
            _incrementalSearch.OnSearchEnd(searchResult =>
            {
                Assert.True(SearchKind.BackwardWithWrap == searchResult.SearchData.Kind);
                didRun = true;
            });
            Process("?cat");
            Assert.True(didRun);
        }

        [WpfFact]
        public void IncrementalSearch_BackwardShouldRespectNoWrapScan()
        {
            _textView.SetText("cat dog");
            _localSettings.GlobalSettings.WrapScan = false;
            var didRun = false;
            _incrementalSearch.OnSearchEnd(searchResult =>
            {
                Assert.True(SearchKind.Backward == searchResult.SearchData.Kind);
                didRun = true;
            });
            Process("?cat");
            Assert.True(didRun);
        }

        /// <summary>
        /// Incremental search input should be mapped via the command mapping.  Documentation
        /// specifies language mapping but implementation dictates command mapping
        /// </summary>
        [WpfFact]
        public void IncrementalSearch_ShouldUseCommandMapping()
        {
            _textView.SetText("cat dog");
            var result = _capture.GetMotionAndCount(KeyInputUtil.CharToKeyInput('/'));
            Assert.True(result.IsNeedMoreInput);
            Assert.Equal(result.AsNeedMoreInput().BindData.KeyRemapMode, KeyRemapMode.Command);
        }

        /// <summary>
        /// Incremental search input should be mapped via the command mapping.  Documentation
        /// specifies language mapping but implementation dictates command mapping
        /// </summary>
        [WpfFact]
        public void IncrementalSearch_ShouldUseCommandMappingForAll()
        {
            _textView.SetText("cat dog");
            var result = _capture.GetMotionAndCount(KeyInputUtil.CharToKeyInput('/'));
            result = result.AsNeedMoreInput().BindData.BindFunction.Invoke(KeyInputUtil.CharToKeyInput('a'));
            Assert.True(result.IsNeedMoreInput);
            Assert.Equal(result.AsNeedMoreInput().BindData.KeyRemapMode, KeyRemapMode.Command);
        }

        [WpfFact]
        public void LineDownToFirstNonBlank_ShouldAcceptBothEnters()
        {
            _textView.SetText("cat\ndog\nbear");
            Assert.True(_capture.GetMotionAndCount(KeyInputUtil.EnterKey).IsComplete);
        }

        [WpfFact]
        public void LineDown()
        {
            AssertMotion(KeyNotationUtil.StringToKeyInput("<c-j>"), Motion.LineDown);
            AssertMotion(KeyNotationUtil.StringToKeyInput("<c-n>"), Motion.LineDown);
            AssertMotion("j", Motion.LineDown);
            AssertMotion(VimKey.Down, Motion.LineDown);
            AssertMotion(VimKey.LineFeed, Motion.LineDown);
        }

        [WpfFact]
        public void CommandMapSupportsAlternateKeys()
        {
            Assert.True(MapModule.TryFind(new KeyInputSet(KeyInputUtil.EnterKey), _captureRaw.MotionBindingsMap).IsSome());
        }

        [WpfFact]
        public void Mark()
        {
            AssertMotion("`a", Motion.NewMark(LocalMark.OfChar('a').Value));
            AssertMotion("`b", Motion.NewMark(LocalMark.OfChar('b').Value));
        }

        [WpfFact]
        public void MarkLine()
        {
            AssertMotion("'a", Motion.NewMarkLine(LocalMark.OfChar('a').Value));
            AssertMotion("'b", Motion.NewMarkLine(LocalMark.OfChar('b').Value));
        }

        /// <summary>
        /// Make sure the bindings aren't incorrectly structured such that the incremental search
        /// begins on MotionCapture startup.  It should only begin during the processing of a motion
        /// </summary>
        [WpfFact]
        public void Search_EnsureIncrementalSearchNotStarted()
        {
            Assert.False(_incrementalSearch.HasActiveSession);
        }

        /// <summary>
        /// Search should begin once the '/' is processed
        /// </summary>
        [WpfFact]
        public void Search_EnsureStartedOnSlash()
        {
            _capture.GetMotionAndCount('/');
            Assert.True(_incrementalSearch.HasActiveSession);
        }

        /// <summary>
        /// Escape should end the search operation
        /// </summary>
        [WpfFact]
        public void Search_EscapeShouldEndTheSearch()
        {
            var result = _capture.GetMotionAndCount('/');
            Assert.True(result.IsNeedMoreInput);
            result.AsNeedMoreInput().BindData.BindFunction.Invoke(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape));
            Assert.False(_incrementalSearch.HasActiveSession);
        }

        [WpfFact]
        public void ForceLineWise()
        {
            AssertMotion("Vl", Motion.NewForceLineWise(Motion.CharRight));
            AssertMotion("Vh", Motion.NewForceLineWise(Motion.CharLeft));
        }

        [WpfFact]
        public void ForceCharacterWise()
        {
            AssertMotion("vl", Motion.NewForceCharacterWise(Motion.CharRight));
            AssertMotion("vh", Motion.NewForceCharacterWise(Motion.CharLeft));
        }
    }
}
