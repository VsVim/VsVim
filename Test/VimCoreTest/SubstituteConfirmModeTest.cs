using System;
using EditorUtils;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using Xunit;
using Vim.Extensions;
using Vim.Modes.SubstituteConfirm;

namespace Vim.UnitTest
{
    public sealed class SubstituteConfirmModeTest : VimTestBase
    {
        private MockRepository _factory;
        private ITextView _textView;
        private Mock<ICommonOperations> _operations;
        private Mock<IEditorOperations> _editorOperations;
        private ITextBuffer _textBuffer;
        private SubstituteConfirmMode _modeRaw;
        private ISubstituteConfirmMode _mode;

        public void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _factory = new MockRepository(MockBehavior.Strict);
            _editorOperations = _factory.Create<IEditorOperations>();
            _operations = _factory.Create<ICommonOperations>();
            _operations.Setup(x => x.MoveCaretToPoint(It.IsAny<SnapshotPoint>(), It.IsAny<ViewFlags>()));
            _operations.Setup(x => x.GetReplaceData(It.IsAny<SnapshotPoint>())).Returns(new ReplaceData(Environment.NewLine, Vim.GlobalSettings.Magic, 1));
            _operations.SetupGet(x => x.EditorOperations).Returns(_editorOperations.Object);

            var vimBufferData = CreateVimBufferData(_textView);
            _modeRaw = new SubstituteConfirmMode(vimBufferData, _operations.Object);
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
            Assert.True(didSee);
            Assert.Equal(expectedOption, _mode.CurrentMatch);
        }

        [Fact]
        public void ModeKind_IsCorrect()
        {
            Create();
            Assert.Equal(ModeKind.SubstituteConfirm, _mode.ModeKind);
        }

        /// <summary>
        /// In practice this shouldn't happen but guard against a coding error here
        /// </summary>
        [Fact]
        public void OnEnter_NoArgument()
        {
            Create();
            _mode.OnEnter(ModeArgument.None);
            Assert.True(_mode.CurrentMatch.IsNone());
            Assert.True(_mode.CurrentSubstitute.IsNone());
        }

        [Fact]
        public void OnEnter_StandardArgument()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "a", "b"));
            Assert.Equal(_textBuffer.GetLine(0).Extent, _mode.CurrentMatch.Value);
        }

        [Fact]
        public void InputYes_OnlyOneMatchInBuffer()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            _mode.Process('y');
            Assert.Equal("bird", _textBuffer.GetLine(0).GetText());
        }

        [Fact]
        public void InputYes_OnlyOneMatchInRange()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird", range: _textBuffer.GetLineRange(0)));
            Assert.True(_mode.Process('y').IsSwitchMode(ModeKind.Normal));
            Assert.Equal("bird", _textBuffer.GetLine(0).GetText());
            Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
        }

        [Fact]
        public void InputYes_TwoMatchesOnLineButNotReplaceAll()
        {
            Create("cat cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetSpan(0, 3), "cat", "bird", range: _textBuffer.GetLineRange(0)));
            Assert.True(_mode.Process('y').IsSwitchMode(ModeKind.Normal));
            Assert.Equal("bird cat", _textBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// Should replace and move to next match
        /// </summary>
        [Fact]
        public void InputYes_TwoMatchesOnLineAndReplaceAll()
        {
            Create("cat cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetSpan(0, 3), "cat", "bird", SubstituteFlags.ReplaceAll, range: _textBuffer.GetLineRange(0)));
            _operations.Setup(x => x.MoveCaretToPoint(_textBuffer.GetPoint(5), ViewFlags.Standard)).Verifiable();
            Assert.True(_mode.Process('y').IsHandledNoSwitch());
            Assert.Equal("bird cat", _textBuffer.GetLine(0).GetText());
            Assert.Equal(_textBuffer.GetSpan(5, 3), _mode.CurrentMatch.Value);
            _factory.Verify();
        }

        /// <summary>
        /// Should replace and move to next match
        /// </summary>
        [Fact]
        public void InputYes_TwoMatchesInRange()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird", SubstituteFlags.ReplaceAll, range: _textBuffer.GetLineRange(0, 1)));
            Assert.True(_mode.Process('y').IsHandledNoSwitch());
            Assert.Equal("bird", _textBuffer.GetLine(0).GetText());
            Assert.Equal(_textBuffer.GetLine(1).Extent, _mode.CurrentMatch.Value);
        }

        [Fact]
        public void InputEscape_InMatch()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.True(_mode.Process(KeyInputUtil.EscapeKey).IsSwitchMode(ModeKind.Normal));
        }

        [Fact]
        public void InputQuit_InMatch()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.True(_mode.Process('q').IsSwitchMode(ModeKind.Normal));
        }

        [Fact]
        public void InputLast_OnlyOneMatch()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.True(_mode.Process('l').IsSwitchMode(ModeKind.Normal));
            Assert.Equal("bird", _textBuffer.GetLine(0).GetText());
        }

        [Fact]
        public void InputLast_TwoMatchesInRange()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.True(_mode.Process('l').IsSwitchMode(ModeKind.Normal));
            Assert.Equal("bird", _textBuffer.GetLine(0).GetText());
            Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
        }

        [Fact]
        public void InputNo_OnlyOneMatch()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.True(_mode.Process('n').IsSwitchMode(ModeKind.Normal));
            Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
            Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
        }

        [Fact]
        public void InputNo_TwoMatchesInRange()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.True(_mode.Process('n').IsHandledNoSwitch());
            Assert.Equal(_textBuffer.GetLine(1).Extent, _mode.CurrentMatch.Value);
            Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
            Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
        }

        [Fact]
        public void InputAll_OnlyOneMatch()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.True(_mode.Process('a').IsSwitchMode(ModeKind.Normal));
            Assert.Equal("bird", _textBuffer.GetLine(0).GetText());
            Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
        }

        [Fact]
        public void InputAll_TwoMatchesInRange()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            Assert.True(_mode.Process('a').IsSwitchMode(ModeKind.Normal));
            Assert.Equal("bird", _textBuffer.GetLine(0).GetText());
            Assert.Equal("bird", _textBuffer.GetLine(1).GetText());
        }

        [Fact]
        public void CurrentMatchChanged_OnEnter()
        {
            Create("cat", "cat", "rabbit", "tree");
            VeriyCurrentMatchChanged(
                () => _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent)),
                _textBuffer.GetLine(0).Extent);
        }

        [Fact]
        public void CurrentMatchChanged_OnYesEnds()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            VeriyCurrentMatchChanged(
                () => { _mode.Process('y'); },
                expected: null);
        }

        [Fact]
        public void CurrentMatchChanged_OnYesGoesToNext()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            VeriyCurrentMatchChanged(
                () => { _mode.Process('y'); },
                () => _textBuffer.GetLine(1).Extent);
        }

        [Fact]
        public void CurrentMatchChanged_OnNoEnds()
        {
            Create("cat", "dog", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            VeriyCurrentMatchChanged(() => { _mode.Process('n'); });
        }

        [Fact]
        public void CurrentMatchChanged_OnNoGoesToNext()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            VeriyCurrentMatchChanged(() => { _mode.Process('n'); }, _textBuffer.GetLine(1).Extent);
        }

        [Fact]
        public void CurrentMatchChanged_OnAll()
        {
            Create("cat", "cat", "rabbit", "tree");
            _mode.OnEnter(VimUtil.CreateSubstituteArgument(_textBuffer.GetLine(0).Extent, "cat", "bird"));
            VeriyCurrentMatchChanged(() => { _mode.Process('a'); });
        }
    }
}
