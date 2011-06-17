using System;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes;
using Vim.Modes.SubstituteConfirm;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class SubstituteConfirmModeTest
    {
        private MockRepository _factory;
        private Mock<ITextCaret> _textCaret;
        private Mock<ITextView> _textView;
        private Mock<IVimBuffer> _buffer;
        private Mock<ICommonOperations> _operations;
        private Mock<IEditorOperations> _editorOperations;
        private ITextBuffer _textBuffer;
        private SubstituteConfirmMode _modeRaw;
        private ISubstituteConfirmMode _mode;

        public void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _textCaret = _factory.Create<ITextCaret>();
            _textView = MockObjectFactory.CreateTextView(textBuffer: _textBuffer, caret: _textCaret.Object, factory: _factory);
            _buffer = MockObjectFactory.CreateVimBuffer(textView: _textView.Object, factory: _factory);
            _editorOperations = _factory.Create<IEditorOperations>();
            _operations = _factory.Create<ICommonOperations>();
            _operations.Setup(x => x.MoveCaretToPoint(It.IsAny<SnapshotPoint>()));
            _operations.Setup(x => x.MoveCaretToPointAndEnsureVisible(It.IsAny<SnapshotPoint>()));
            _operations.SetupGet(x => x.EditorOperations).Returns(_editorOperations.Object);
            _modeRaw = new SubstituteConfirmMode(_buffer.Object, _operations.Object);
            _mode = _modeRaw;
        }

        private void VeriyCurrentMatchChanged(Action action, SnapshotSpan? expected = null)
        {
            VeriyCurrentMatchChanged(action, () => expected);
        }

        private void VeriyCurrentMatchChanged(Action action, Func<SnapshotSpan?> expectedFunc)
        {
            var didSee = false;
            FSharpOption<SnapshotSpan> saw = FSharpOption<SnapshotSpan>.None;
            _mode.CurrentMatchChanged += (sender, e) =>
            {
                didSee = true;
                saw = e;
            };
            action();

            var expected = expectedFunc();
            var expectedOption = expected.HasValue
                ? FSharpOption.Create(expected.Value)
                : FSharpOption<SnapshotSpan>.None;
            Assert.IsTrue(didSee);
            Assert.AreEqual(expectedOption, _mode.CurrentMatch);
        }

        [Test]
        public void ModeKind_IsCorrect()
        {
            Create();
            Assert.AreEqual(ModeKind.SubstituteConfirm, _mode.ModeKind);
        }

        [Test]
        [Description("In practice this shouldn't happen but guard against a coding error here")]
        public void OnEnter_NoArgument()
        {
            Create();
            _mode.OnEnter(ModeArgument.None);
            Assert.IsTrue(_mode.CurrentMatch.IsNone());
            Assert.IsTrue(_mode.CurrentSubstitute.IsNone());
        }

        [Test]
        public void OnEnter_StandardArgument()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "a", "b"));
            Assert.AreEqual(_textBuffer.GetLine(0).Extent, _mode.CurrentMatch.Value);
        }

        [Test]
        public void InputYes_OnlyOneMatchInBuffer()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            _mode.Process('y');
            Assert.AreEqual("bird", _textBuffer.GetLine(0).GetText());
        }

        [Test]
        public void InputYes_OnlyOneMatchInRange()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird", range: _textBuffer.GetLineRange(0)));
            Assert.IsTrue(_mode.Process('y').IsSwitchMode(ModeKind.Normal));
            Assert.AreEqual("bird", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("cat", _textBuffer.GetLine(1).GetText());
        }

        [Test]
        public void InputYes_TwoMatchesOnLineButNotReplaceAll()
        {
            Create("cat cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetSpan(0, 3), "cat", "bird", range: _textBuffer.GetLineRange(0)));
            Assert.IsTrue(_mode.Process('y').IsSwitchMode(ModeKind.Normal));
            Assert.AreEqual("bird cat", _textBuffer.GetLine(0).GetText());
        }

        [Test]
        [Description("Should replace and move to next match")]
        public void InputYes_TwoMatchesOnLineAndReplaceAll()
        {
            Create("cat cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetSpan(0, 3), "cat", "bird", SubstituteFlags.ReplaceAll, range: _textBuffer.GetLineRange(0)));
            _operations.Setup(x => x.MoveCaretToPointAndEnsureVisible(_textBuffer.GetPoint(5))).Verifiable();
            Assert.IsTrue(_mode.Process('y').IsHandledNoSwitch());
            Assert.AreEqual("bird cat", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual(_textBuffer.GetSpan(5, 3), _mode.CurrentMatch.Value);
            _factory.Verify();
        }

        [Test]
        [Description("Should replace and move to next match")]
        public void InputYes_TwoMatchesInRange()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird", SubstituteFlags.ReplaceAll, range: _textBuffer.GetLineRange(0, 1)));
            Assert.IsTrue(_mode.Process('y').IsHandledNoSwitch());
            Assert.AreEqual("bird", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual(_textBuffer.GetLine(1).Extent, _mode.CurrentMatch.Value);
        }

        [Test]
        public void InputEscape_InMatch()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.IsTrue(_mode.Process(KeyInputUtil.EscapeKey).IsSwitchMode(ModeKind.Normal));
        }

        [Test]
        public void InputQuit_InMatch()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.IsTrue(_mode.Process('q').IsSwitchMode(ModeKind.Normal));
        }

        [Test]
        public void InputLast_OnlyOneMatch()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.IsTrue(_mode.Process('l').IsSwitchMode(ModeKind.Normal));
            Assert.AreEqual("bird", _textBuffer.GetLine(0).GetText());
        }

        [Test]
        public void InputLast_TwoMatchesInRange()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.IsTrue(_mode.Process('l').IsSwitchMode(ModeKind.Normal));
            Assert.AreEqual("bird", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("cat", _textBuffer.GetLine(1).GetText());
        }

        [Test]
        public void InputNo_OnlyOneMatch()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.IsTrue(_mode.Process('n').IsSwitchMode(ModeKind.Normal));
            Assert.AreEqual("cat", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("dog", _textBuffer.GetLine(1).GetText());
        }

        [Test]
        public void InputNo_TwoMatchesInRange()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.IsTrue(_mode.Process('n').IsHandledNoSwitch());
            Assert.AreEqual(_textBuffer.GetLine(1).Extent, _mode.CurrentMatch.Value);
            Assert.AreEqual("cat", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("cat", _textBuffer.GetLine(1).GetText());
        }

        [Test]
        public void InputAll_OnlyOneMatch()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.IsTrue(_mode.Process('a').IsSwitchMode(ModeKind.Normal));
            Assert.AreEqual("bird", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("dog", _textBuffer.GetLine(1).GetText());
        }

        [Test]
        public void InputAll_TwoMatchesInRange()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.IsTrue(_mode.Process('a').IsSwitchMode(ModeKind.Normal));
            Assert.AreEqual("bird", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("bird", _textBuffer.GetLine(1).GetText());
        }

        [Test]
        public void CurrentMatchChanged_OnEnter()
        {
            Create("cat", "cat", "rabbit", "tree");
            VeriyCurrentMatchChanged(
                () => _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent)),
                _textBuffer.GetLine(0).Extent);
        }

        [Test]
        public void CurrentMatchChanged_OnYesEnds()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            VeriyCurrentMatchChanged(
                () => { _mode.Process('y'); },
                expected: null);
        }

        [Test]
        public void CurrentMatchChanged_OnYesGoesToNext()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            VeriyCurrentMatchChanged(
                () => { _mode.Process('y'); },
                () => _textBuffer.GetLine(1).Extent);
        }

        [Test]
        public void CurrentMatchChanged_OnNoEnds()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            VeriyCurrentMatchChanged(() => { _mode.Process('n'); });
        }

        [Test]
        public void CurrentMatchChanged_OnNoGoesToNext()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            VeriyCurrentMatchChanged(() => { _mode.Process('n'); }, _textBuffer.GetLine(1).Extent);
        }

        [Test]
        public void CurrentMatchChanged_OnAll()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            VeriyCurrentMatchChanged(() => { _mode.Process('a'); });
        }
    }
}
