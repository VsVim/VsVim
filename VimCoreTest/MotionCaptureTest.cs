using System.Linq;
using EditorUtils.UnitTest;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    [TestFixture]
    public class MotionCaptureTest : VimTestBase
    {
        private IVimLocalSettings _localSettings;
        private ITextView _textView;
        private IIncrementalSearch _incrementalSearch;
        private MotionCapture _captureRaw;
        private IMotionCapture _capture;

        [SetUp]
        public void SetUp()
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
                Assert.IsTrue(res.IsNeedMoreInput);
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
            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(motion, result.AsComplete().Item);
        }

        private void AssertMotion(KeyInput keyInput, Motion motion)
        {
            var result = _capture.GetMotionAndCount(keyInput);
            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(motion, result.AsComplete().Item.Item1);
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

        [Test]
        public void Word()
        {
            AssertMotion("w", Motion.NewWordForward(WordKind.NormalWord));
            AssertMotion("W", Motion.NewWordForward(WordKind.BigWord));
        }

        [Test]
        public void BadInput()
        {
            var res = Process("z");
            Assert.IsTrue(res.IsError);
        }

        [Test]
        public void EndOfLine()
        {
            AssertMotion("$", Motion.EndOfLine);
            AssertMotion(VimKey.End, Motion.EndOfLine);
        }

        [Test]
        public void BeginingOfLine()
        {
            AssertMotion("0", Motion.BeginingOfLine);
        }

        [Test]
        public void AllWord()
        {
            AssertMotion("aw", Motion.NewAllWord(WordKind.NormalWord));
            AssertMotion("aW", Motion.NewAllWord(WordKind.BigWord));
        }

        [Test]
        public void LineFromTopOfWindow()
        {
            AssertMotion("H", Motion.LineFromTopOfVisibleWindow);
        }

        [Test]
        public void CharLeft()
        {
            AssertMotion("h", Motion.CharLeft);
            AssertMotion(VimKey.Left, Motion.CharLeft);
            AssertMotion(VimKey.Back, Motion.CharLeft);
            AssertMotion(KeyNotationUtil.StringToKeyInput("<C-h>"), Motion.CharLeft);
        }

        [Test]
        public void CharRight()
        {
            AssertMotion("l", Motion.CharRight);
            AssertMotion(VimKey.Right, Motion.CharRight);
            AssertMotion(VimKey.Space, Motion.CharRight);
        }

        [Test]
        public void LineUp()
        {
            AssertMotion("k", Motion.LineUp);
            AssertMotion(VimKey.Up, Motion.LineUp);
            AssertMotion(KeyNotationUtil.StringToKeyInput("<C-p>"), Motion.LineUp);
        }

        [Test]
        public void EndOfWord()
        {
            AssertMotion("e", Motion.NewEndOfWord(WordKind.NormalWord));
            AssertMotion("E", Motion.NewEndOfWord(WordKind.BigWord));
        }

        [Test]
        public void CharSearch_ToCharForward()
        {
            AssertMotion("fc", Motion.NewCharSearch(CharSearchKind.ToChar, Path.Forward, 'c'));
        }

        [Test]
        public void CharSearch_TillCharForward()
        {
            AssertMotion("tc", Motion.NewCharSearch(CharSearchKind.TillChar, Path.Forward, 'c'));
        }

        [Test]
        public void CharSearch_ToCharBackward()
        {
            AssertMotion("Fc", Motion.NewCharSearch(CharSearchKind.ToChar, Path.Backward, 'c'));
        }

        [Test]
        public void CharSearch_TillCharBackward()
        {
            AssertMotion("Tc", Motion.NewCharSearch(CharSearchKind.TillChar, Path.Backward, 'c'));
        }

        [Test]
        public void LineOrLastToFirstNonBlank()
        {
            AssertMotion("G", Motion.LineOrLastToFirstNonBlank);
        }

        [Test]
        public void LineOrFirstToFirstNonBlank()
        {
            AssertMotion("gg", Motion.LineOrFirstToFirstNonBlank);
        }

        [Test]
        public void LastNonBlankOnLine()
        {
            AssertMotion("g_", Motion.LastNonBlankOnLine);
        }

        [Test]
        public void LineInMiddleOfVisibleWindow()
        {
            AssertMotion("M", Motion.LineInMiddleOfVisibleWindow);
        }

        [Test]
        public void LineFromBottomOfVisibleWindow()
        {
            AssertMotion("L", Motion.LineFromBottomOfVisibleWindow);
        }

        [Test]
        public void FirstNonBlankOnLine()
        {
            AssertMotion("_", Motion.FirstNonBlankOnLine);
        }

        [Test]
        public void FirstNonBlankOnLineOnCurrentLine()
        {
            AssertMotion("^", Motion.FirstNonBlankOnCurrentLine);
        }

        [Test]
        public void RepeatLastCharSearch()
        {
            AssertMotion(";", Motion.RepeatLastCharSearch);
            AssertMotion(",", Motion.RepeatLastCharSearchOpposite);
        }

        [Test]
        public void SentenceForward()
        {
            AssertMotion(")", Motion.SentenceForward);
        }

        [Test]
        public void SentenceBackward()
        {
            AssertMotion("(", Motion.SentenceBackward);
        }

        [Test]
        public void AllSentence()
        {
            AssertMotion("as", Motion.AllSentence);
        }

        [Test]
        public void ParagraphForward()
        {
            AssertMotion("}", Motion.ParagraphForward);
        }

        [Test]
        public void ParagraphBackward()
        {
            AssertMotion("{", Motion.ParagraphBackward);
        }

        [Test]
        public void SectionForwardOrOpenBrace()
        {
            AssertMotion("]]", Motion.SectionForward);
        }

        [Test]
        public void SectionForwardOrCloseBrace()
        {
            AssertMotion("][", Motion.SectionForwardOrCloseBrace);
        }

        [Test]
        public void SectionBackwardOrOpenBrace()
        {
            AssertMotion("[[", Motion.SectionBackwardOrOpenBrace);
        }

        [Test]
        public void SectionBackwardOrCloseBrace()
        {
            AssertMotion("[]", Motion.SectionBackwardOrCloseBrace);
        }

        [Test]
        public void QuotedString()
        {
            AssertMotion(@"a""", Motion.NewQuotedString('"'));
            AssertMotion("a'", Motion.NewQuotedString('\''));
            AssertMotion("a`", Motion.NewQuotedString('`'));
        }

        [Test]
        public void QuotedStringContents1()
        {
            AssertMotion(@"i""", Motion.NewQuotedStringContents('"'));
            AssertMotion("i'", Motion.NewQuotedStringContents('\''));
            AssertMotion("i`", Motion.NewQuotedStringContents('`'));
        }

        [Test]
        public void IncrementalSearch_Reverse()
        {
            _textView.TextBuffer.SetText("hello world");
            _textView.MoveCaretTo(_textView.GetEndPoint().Position);
            var data = Process("?world", enter: true).AsComplete().Item;
            var patternData = VimUtil.CreatePatternData("world", Path.Backward);
            Assert.AreEqual(Motion.NewSearch(patternData), data);
        }

        [Test]
        public void IncrementalSearch_Forward()
        {
            _textView.SetText("hello world", caret: 0);
            var data = Process("/world", enter: true).AsComplete().Item;
            var patternData = VimUtil.CreatePatternData("world", Path.Forward);
            Assert.AreEqual(Motion.NewSearch(patternData), data);
        }

        [Test]
        public void IncrementalSearch_ForwardShouldRespectWrapScan()
        {
            _textView.SetText("cat dog");
            var didRun = false;
            _incrementalSearch.CurrentSearchUpdated += (_, args) =>
            {
                Assert.IsTrue(SearchKind.ForwardWithWrap == args.SearchResult.SearchData.Kind);
                didRun = true;
            };
            Process("/cat");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void IncrementalSearch_ForwardShouldRespectNoWrapScan()
        {
            _textView.SetText("cat dog");
            _localSettings.GlobalSettings.WrapScan = false;
            var didRun = false;
            _incrementalSearch.CurrentSearchUpdated += (_, args) =>
            {
                Assert.IsTrue(SearchKind.Forward == args.SearchResult.SearchData.Kind);
                didRun = true;
            };
            Process("/cat");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void IncrementalSearch_BackwardShouldRespectWrapScan()
        {
            _textView.SetText("cat dog");
            var didRun = false;
            _incrementalSearch.CurrentSearchUpdated += (_, args) =>
            {
                Assert.IsTrue(SearchKind.BackwardWithWrap == args.SearchResult.SearchData.Kind);
                didRun = true;
            };
            Process("?cat");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void IncrementalSearch_BackwardShouldRespectNoWrapScan()
        {
            _textView.SetText("cat dog");
            _localSettings.GlobalSettings.WrapScan = false;
            var didRun = false;
            _incrementalSearch.CurrentSearchUpdated += (_, args) =>
            {
                Assert.IsTrue(SearchKind.Backward == args.SearchResult.SearchData.Kind);
                didRun = true;
            };
            Process("?cat");
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Incremental search input should be mapped via the command mapping.  Documentation
        /// specifies language mapping but implementation dictates command mapping
        /// </summary>
        [Test]
        public void IncrementalSearch_ShouldUseCommandMapping()
        {
            _textView.SetText("cat dog");
            var result = _capture.GetMotionAndCount(KeyInputUtil.CharToKeyInput('/'));
            Assert.IsTrue(result.IsNeedMoreInput);
            Assert.IsTrue(result.AsNeedMoreInput().Item.KeyRemapMode.IsSome(KeyRemapMode.Command));
        }

        /// <summary>
        /// Incremental search input should be mapped via the command mapping.  Documentation
        /// specifies language mapping but implementation dictates command mapping
        /// </summary>
        [Test]
        public void IncrementalSearch_ShouldUseCommandMappingForAll()
        {
            _textView.SetText("cat dog");
            var result = _capture.GetMotionAndCount(KeyInputUtil.CharToKeyInput('/'));
            result = result.AsNeedMoreInput().Item.BindFunction.Invoke(KeyInputUtil.CharToKeyInput('a'));
            Assert.IsTrue(result.IsNeedMoreInput);
            Assert.IsTrue(result.AsNeedMoreInput().Item.KeyRemapMode.IsSome(KeyRemapMode.Command));
        }

        [Test]
        public void LineDownToFirstNonBlank_ShouldAcceptBothEnters()
        {
            _textView.SetText("cat\ndog\nbear");
            Assert.IsTrue(_capture.GetMotionAndCount(KeyInputUtil.AlternateEnterKey).IsComplete);
            Assert.IsTrue(_capture.GetMotionAndCount(KeyInputUtil.EnterKey).IsComplete);
        }

        [Test]
        public void CommandMapSupportsAlternateKeys()
        {
            Assert.IsTrue(MapModule.TryFind(KeyInputSet.NewOneKeyInput(KeyInputUtil.AlternateEnterKey), _captureRaw.MotionBindingsMap).IsSome());
            Assert.IsTrue(MapModule.TryFind(KeyInputSet.NewOneKeyInput(KeyInputUtil.EnterKey), _captureRaw.MotionBindingsMap).IsSome());
        }

        [Test]
        public void Mark()
        {
            AssertMotion("`a", Motion.NewMark(LocalMark.OfChar('a').Value));
            AssertMotion("`b", Motion.NewMark(LocalMark.OfChar('b').Value));
        }

        [Test]
        public void MarkLine()
        {
            AssertMotion("'a", Motion.NewMarkLine(LocalMark.OfChar('a').Value));
            AssertMotion("'b", Motion.NewMarkLine(LocalMark.OfChar('b').Value));
        }

        /// <summary>
        /// Make sure the bindings aren't incorrectly structured such that the incremental search
        /// begins on MotionCapture startup.  It should only begin during the processing of a motion
        /// </summary>
        [Test]
        public void Search_EnsureIncrementalSearchNotStarted()
        {
            Assert.IsFalse(_incrementalSearch.InSearch);
        }

        /// <summary>
        /// Search should begin once the '/' is processed
        /// </summary>
        [Test]
        public void Search_EnsureStartedOnSlash()
        {
            _capture.GetMotionAndCount('/');
            Assert.IsTrue(_incrementalSearch.InSearch);
        }

        /// <summary>
        /// Escape should end the search operation
        /// </summary>
        [Test]
        public void Search_EscapeShouldEndTheSearch()
        {
            var result = _capture.GetMotionAndCount('/');
            Assert.IsTrue(result.IsNeedMoreInput);
            result.AsNeedMoreInput().Item.BindFunction.Invoke(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape));
            Assert.IsFalse(_incrementalSearch.InSearch);
        }

    }
}
