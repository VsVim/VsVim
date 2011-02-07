using System.Linq;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class MotionCaptureTest
    {
        private MockRepository _factory;
        private Mock<IVimHost> _host;
        private IVimLocalSettings _localSettings;
        private ITextView _textView;
        private IIncrementalSearch _incrementalSearch;
        private MotionCapture _captureRaw;
        private IMotionCapture _capture;

        [SetUp]
        public void SetUp()
        {
            _textView = EditorUtil.CreateView();
            _localSettings = new LocalSettings(new GlobalSettings(), _textView);
            _incrementalSearch = VimUtil.CreateIncrementalSearch(_textView, _localSettings, new VimData());
            _factory = new MockRepository(MockBehavior.Strict);
            _host = _factory.Create<IVimHost>();
            _captureRaw = new MotionCapture(
                _host.Object,
                _textView,
                _incrementalSearch,
                _localSettings);
            _capture = _captureRaw;
        }

        internal MotionResult Process(string input, int? count = 1, bool enter = false)
        {
            var realCount = count.HasValue
                ? FSharpOption.Create(count.Value)
                : FSharpOption<int>.None;
            var res = _capture.GetOperatorMotion(
                KeyInputUtil.CharToKeyInput(input[0]),
                realCount);
            foreach (var cur in input.Skip(1))
            {
                Assert.IsTrue(res.IsNeedMoreInput);
                var needMore = (MotionResult.NeedMoreInput)res;
                res = needMore.Item2.Invoke(KeyInputUtil.CharToKeyInput(cur));
            }

            if (enter)
            {
                var needMore = (MotionResult.NeedMoreInput)res;
                res = needMore.Item2.Invoke(KeyInputUtil.EnterKey);
            }

            return res;
        }

        private void ProcessComplete(string input, int? count = null)
        {
            Assert.IsTrue(Process(input, count).IsComplete);
        }

        private void AssertMotion(string text, Motion motion)
        {
            var result = Process(text);
            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(motion, result.AsComplete().Item1.Motion);
        }

        private void AssertMotion(KeyInput keyInput, Motion motion)
        {
            var result = _capture.GetOperatorMotion(keyInput, FSharpOption<int>.None);
            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(motion, result.AsComplete().Item1.Motion);
        }

        private void AssertMotion(VimKey key, Motion motion)
        {
            AssertMotion(KeyInputUtil.VimKeyToKeyInput(key), motion);
        }

        private static MotionData CreateMotionData()
        {
            var point = MockObjectFactory.CreateSnapshotPoint(42);
            return VimUtil.CreateMotionData(
                new SnapshotSpan(point, point),
                true,
                MotionKind.Inclusive,
                OperationKind.CharacterWise,
                42);
        }

        private static FSharpOption<MotionData> CreateMotionDataSome()
        {
            return FSharpOption.Create(CreateMotionData());
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
            var res = Process("z", 1);
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
        public void FirstNonWhitespaceOnLine()
        {
            AssertMotion("^", Motion.FirstNonWhiteSpaceOnLine);
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
            AssertMotion("fc", Motion.NewCharSearch(CharSearchKind.ToChar, Direction.Forward, 'c'));
        }

        [Test]
        public void CharSearch_TillCharForward()
        {
            AssertMotion("tc", Motion.NewCharSearch(CharSearchKind.TillChar, Direction.Forward, 'c'));
        }

        [Test]
        public void CharSearch_ToCharBackward()
        {
            AssertMotion("Fc", Motion.NewCharSearch(CharSearchKind.ToChar, Direction.Backward, 'c'));
        }

        [Test]
        public void CharSearch_TillCharBackward()
        {
            AssertMotion("Tc", Motion.NewCharSearch(CharSearchKind.TillChar, Direction.Backward, 'c'));
        }

        [Test]
        public void LineOrLastToFirstNonWhiteSpace()
        {
            AssertMotion("G", Motion.LineOrLastToFirstNonWhiteSpace);
        }

        [Test]
        public void LineOrFirstToFirstNonWhiteSpace()
        {
            AssertMotion("gg", Motion.LineOrFirstToFirstNonWhiteSpace);
        }

        [Test]
        public void LastNonWhiteSpaceOnLine()
        {
            AssertMotion("g_", Motion.LastNonWhiteSpaceOnLine);
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
        public void LineDownToFirstNonWhiteSpace()
        {
            AssertMotion("_", Motion.LineDownToFirstNonWhiteSpace);
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
            AssertMotion("aw", Motion.AllSentence);
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
            AssertMotion("]]", Motion.SectionForwardOrOpenBrace);
        }

        [Test]
        public void SectionForwardOrCloseBrace()
        {
            AssertMotion("][", Motion.SectionForwardOrOpenBrace);
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
            AssertMotion(@"a""", Motion.QuotedString);
            AssertMotion("a'", Motion.QuotedString);
            AssertMotion("a`", Motion.QuotedString);
        }

        [Test]
        public void QuotedStringContents1()
        {
            AssertMotion(@"i""", Motion.QuotedStringContents);
            AssertMotion("i'", Motion.QuotedStringContents);
            AssertMotion("i`", Motion.QuotedStringContents);
        }

        [Test]
        public void IncrementalSearch_Reverse()
        {
            _textView.TextBuffer.SetText("hello world");
            _textView.MoveCaretTo(_textView.GetEndPoint().Position);
            var data = Process("?world", enter: true).AsComplete().Item1;
            Assert.AreEqual(Motion.NewSearch("world", SearchKind.BackwardWithWrap), data.Motion);
        }

        [Test]
        public void IncrementalSearch_Forward()
        {
            _textView.SetText("hello world", caret: 0);
            var data = Process("/world", enter: true).AsComplete().Item1;
            Assert.AreEqual(Motion.NewSearch("world", SearchKind.ForwardWithWrap), data.Motion);
        }

        [Test]
        public void IncrementalSearch_ForwardShouldRespectWrapScan()
        {
            _textView.SetText("cat dog");
            var didRun = false;
            _incrementalSearch.CurrentSearchUpdated += (_, args) =>
            {
                Assert.IsTrue(SearchKind.ForwardWithWrap == args.Item1.Kind);
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
                Assert.IsTrue(SearchKind.Forward == args.Item1.Kind);
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
                Assert.IsTrue(SearchKind.BackwardWithWrap == args.Item1.Kind);
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
                Assert.IsTrue(SearchKind.Backward == args.Item1.Kind);
                didRun = true;
            };
            Process("?cat");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void LineDownToFirstNonWhitespace_ShouldAcceptBothEnters()
        {
            _textView.SetText("cat\ndog\nbear");
            Assert.IsTrue(_capture.GetOperatorMotion(KeyInputUtil.AlternateEnterKey, FSharpOption.Create(2)).IsComplete);
            Assert.IsTrue(_capture.GetOperatorMotion(KeyInputUtil.EnterKey, FSharpOption.Create(2)).IsComplete);
        }

        [Test]
        public void CommandMapSupportsAlternateKeys()
        {
            Assert.IsTrue(MapModule.TryFind(KeyInputSet.NewOneKeyInput(KeyInputUtil.AlternateEnterKey), _captureRaw.MotionCommandsMap).IsSome());
            Assert.IsTrue(MapModule.TryFind(KeyInputSet.NewOneKeyInput(KeyInputUtil.EnterKey), _captureRaw.MotionCommandsMap).IsSome());
        }
    }
}
