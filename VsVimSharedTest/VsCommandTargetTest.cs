using System;
using System.Windows.Input;
using EditorUtils;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Xunit;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using VsVim.Implementation;
using VsVim.Implementation.Misc;
using Microsoft.VisualStudio.Text;

namespace VsVim.UnitTest
{
    public abstract class VsCommandTargetTest : VimTestBase
    {
        protected readonly MockRepository _factory;
        protected readonly IVimBuffer _vimBuffer;
        protected readonly IVim _vim;
        protected readonly ITextBuffer _textBuffer;
        protected readonly ITextView _textView;
        protected readonly Mock<IVsAdapter> _vsAdapter;
        protected readonly Mock<IOleCommandTarget> _nextTarget;
        protected readonly Mock<IDisplayWindowBroker> _broker;
        protected readonly Mock<ITextManager> _textManager;
        protected readonly IOleCommandTarget _target;
        internal readonly Mock<IResharperUtil> _resharperUtil;
        internal readonly VsCommandTarget _targetRaw;
        internal readonly IVimBufferCoordinator _bufferCoordinator;

        protected VsCommandTargetTest()
        {
            _textView = CreateTextView("");
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _bufferCoordinator = new VimBufferCoordinator(_vimBuffer);
            _vim = _vimBuffer.Vim;
            _factory = new MockRepository(MockBehavior.Strict);

            // By default Resharper isn't loaded
            _resharperUtil = _factory.Create<IResharperUtil>();
            _resharperUtil.SetupGet(x => x.IsInstalled).Returns(false);

            _nextTarget = _factory.Create<IOleCommandTarget>(MockBehavior.Strict);
            _vsAdapter = _factory.Create<IVsAdapter>();
            _vsAdapter.SetupGet(x => x.KeyboardDevice).Returns(InputManager.Current.PrimaryKeyboardDevice);
            _vsAdapter.Setup(x => x.InAutomationFunction).Returns(false);
            _vsAdapter.Setup(x => x.InDebugMode).Returns(false);
            _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);

            _broker = _factory.Create<IDisplayWindowBroker>(MockBehavior.Loose);
            _textManager = _factory.Create<ITextManager>();

            var oldCommandFilter = _nextTarget.Object;
            var vsTextView = _factory.Create<IVsTextView>(MockBehavior.Loose);
            vsTextView.Setup(x => x.AddCommandFilter(It.IsAny<IOleCommandTarget>(), out oldCommandFilter)).Returns(0);
            var result = VsCommandTarget.Create(
                _bufferCoordinator,
                vsTextView.Object,
                _textManager.Object,
                _vsAdapter.Object,
                _broker.Object,
                _resharperUtil.Object,
                KeyUtil);
            Assert.True(result.IsSuccess);
            _targetRaw = result.Value;
            _target = _targetRaw;
        }

        /// <summary>
        /// Make sure to clear the KeyMap map on tear down so we don't mess up other tests
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            _vim.KeyMap.ClearAll();
        }

        /// <summary>
        /// Run the KeyInput value through Exec
        /// </summary>
        protected void RunExec(KeyInput keyInput)
        {
            OleCommandData data;
            Assert.True(OleCommandUtil.TryConvert(keyInput, out data));
            try
            {
                _target.Exec(data);
            }
            finally
            {
                data.Dispose();
            }
        }

        protected void RunExec(VimKey vimKey)
        {
            RunExec(KeyInputUtil.VimKeyToKeyInput(vimKey));
        }

        /// <summary>
        /// Run the given command as a type char through the Exec function
        /// </summary>
        protected void RunExec(char c)
        {
            var keyInput = KeyInputUtil.CharToKeyInput(c);
            RunExec(keyInput);
        }

        internal void RunExec(EditCommand editCommand)
        {
            var oleCommandData = OleCommandData.Empty;
            try
            {
                Assert.True(OleCommandUtil.TryConvert(editCommand, out oleCommandData));
                _target.Exec(oleCommandData);
            }
            finally
            {
                oleCommandData.Dispose();
            }
        }

        /// <summary>
        /// Run the KeyInput value through QueryStatus.  Returns true if the QueryStatus call
        /// indicated the command was supported
        /// </summary>
        protected bool RunQueryStatus(KeyInput keyInput)
        {
            OleCommandData data;
            Assert.True(OleCommandUtil.TryConvert(keyInput, out data));
            try
            {
                OLECMD command;
                return
                    ErrorHandler.Succeeded(_target.QueryStatus(data, out command)) &&
                    command.cmdf == (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
            }
            finally
            {
                data.Dispose();
            }
        }

        /// <summary>
        /// Run the char through the QueryStatus method
        /// </summary>
        protected bool RunQueryStatus(char c)
        {
            var keyInput = KeyInputUtil.CharToKeyInput(c);
            return RunQueryStatus(keyInput);
        }

        internal static EditCommand CreateEditCommand(EditCommandKind editCommandKind)
        {
            return new EditCommand(KeyInputUtil.CharToKeyInput('i'), editCommandKind, Guid.Empty, 42);
        }

        public sealed class TryConvertTest : VsCommandTargetTest
        {
            private void AssertCannotConvert2K(VSConstants.VSStd2KCmdID id)
            {
                KeyInput ki;
                Assert.False(_targetRaw.TryConvert(VSConstants.VSStd2K, (uint)id, IntPtr.Zero, out ki));
            }

            private void AssertCanConvert2K(VSConstants.VSStd2KCmdID id, KeyInput expected)
            {
                KeyInput ki;
                Assert.True(_targetRaw.TryConvert(VSConstants.VSStd2K, (uint)id, IntPtr.Zero, out ki));
                Assert.Equal(expected, ki);
            }

            [Fact]
            public void Tab()
            {
                AssertCanConvert2K(VSConstants.VSStd2KCmdID.TAB, KeyInputUtil.TabKey);
            }

            [Fact]
            public void InAutomationShouldFail()
            {
                _vsAdapter.Setup(x => x.InAutomationFunction).Returns(true);
                AssertCannotConvert2K(VSConstants.VSStd2KCmdID.TAB);
            }

            [Fact]
            public void InIncrementalSearchShouldFail()
            {
                _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(true);
                AssertCannotConvert2K(VSConstants.VSStd2KCmdID.TAB);
            }
        }

        public sealed class QueryStatusTest : VsCommandTargetTest
        {
            [Fact]
            public void IgnoreEscapeIfCantProcess()
            {
                _vimBuffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
                Assert.False(_vimBuffer.CanProcess(KeyInputUtil.EscapeKey));
                _nextTarget.SetupQueryStatus().Verifiable();
                RunQueryStatus(KeyInputUtil.EscapeKey);
                _factory.Verify();
            }

            [Fact]
            public void EnableEscapeButDontHandleNormally()
            {
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcess(VimKey.Escape));
                Assert.True(RunQueryStatus(KeyInputUtil.EscapeKey));
            }

            /// <summary>
            /// Don't actually run the Escape in the QueryStatus command if we're in visual mode
            /// </summary>
            [Fact]
            public void EnableEscapeAndDontHandleInResharperPlusVisualMode()
            {
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None);
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true).Verifiable();
                RunQueryStatus(KeyInputUtil.EscapeKey);
                Assert.Equal(0, count);
                _factory.Verify();
            }

            /// <summary>
            /// Make sure we process Escape during QueryStatus if we're in insert mode and still pass
            /// it on to R#.  R# will intercept escape and never give it to us and we'll think 
            /// we're still in insert.  
            /// </summary>
            [Fact]
            public void Resharper_EnableAndHandleEscape()
            {
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true).Verifiable();
                Assert.True(RunQueryStatus(KeyInputUtil.EscapeKey));
                Assert.True(_bufferCoordinator.DiscardedKeyInput.IsSome(KeyInputUtil.EscapeKey));
                Assert.Equal(1, count);
                _factory.Verify();
            }

            /// <summary>
            /// When Back is processed as a command make sure we handle it in QueryStatus and hide
            /// it from R#.  Back in R# is used to do special parens delete and we don't want that
            /// overriding a command
            /// </summary>
            [Fact]
            public void Reshaper_BackspaceAsCommand()
            {
                var backKeyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Back);
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true).Verifiable();
                Assert.True(_vimBuffer.CanProcessAsCommand(backKeyInput));
                Assert.False(RunQueryStatus(backKeyInput));
                Assert.True(_bufferCoordinator.DiscardedKeyInput.IsSome(backKeyInput));
                Assert.Equal(1, count);
                _factory.Verify();
            }

            /// <summary>
            /// When Back is processed as an edit make sure we don't special case it and instead let
            /// it go back to R# for processing.  They special case Back during edit to do actions
            /// like matched paren deletion that we want to enable.
            /// </summary>
            [Fact]
            public void Reshaper_BackspaceInInsert()
            {
                var backKeyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Back);
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true).Verifiable();
                Assert.True(_vimBuffer.CanProcessAsCommand(backKeyInput));
                Assert.True(RunQueryStatus(backKeyInput));
                Assert.True(_bufferCoordinator.DiscardedKeyInput.IsNone());
                Assert.Equal(0, count);
                _factory.Verify();
            }

            /// <summary>
            /// Make sure we process Escape during QueryStatus if we're in insert mode.  R# will
            /// intercept escape and never give it to us and we'll think we're still in insert
            /// </summary>
            [Fact]
            public void EnableAndHandleEscapeInResharperPlusExternalEdit()
            {
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true).Verifiable();
                Assert.True(RunQueryStatus(KeyInputUtil.EscapeKey));
                Assert.True(_bufferCoordinator.DiscardedKeyInput.IsSome(KeyInputUtil.EscapeKey));
                Assert.Equal(1, count);
                _factory.Verify();
            }

            /// <summary>
            /// The PageUp key isn't special so don't special case it in R#
            /// </summary>
            [Fact]
            public void Reshaprer_HandlePageUpNormally()
            {
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true).Verifiable();
                Assert.True(RunQueryStatus(KeyInputUtil.VimKeyToKeyInput(VimKey.PageUp)));
                Assert.Equal(0, count);
                _factory.Verify();
            }

            /// <summary>
            /// When Visual Studio is in debug mode R# will attempt to handle the Enter key directly
            /// and do nothing.  Presumably they are doing this because it is an edit command and they
            /// are suppressing it's action.  We want to process this directly though if Vim believes
            /// Enter to be a command and not an edit, for example in normal mode
            /// </summary>
            [Fact]
            public void Resharper_EnterAsCommand()
            {
                _textView.SetText("cat", "dog");
                _textView.MoveCaretTo(0);
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true).Verifiable();
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcessAsCommand(KeyInputUtil.EnterKey));
                Assert.False(RunQueryStatus(KeyInputUtil.EnterKey));
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                Assert.True(_bufferCoordinator.DiscardedKeyInput.IsSome(KeyInputUtil.EnterKey));
                _factory.Verify();
            }

            /// <summary>
            /// If Enter isn't going to be processed as a command then don't special case it
            /// mode for R#.  It would be an edit and we don't want to interfere with R#'s handling 
            /// of edits
            /// </summary>
            [Fact]
            public void Resharper_EnterInInsert()
            {
                _textView.SetText("cat", "dog");
                _textView.MoveCaretTo(0);
                var savedSnapshot = _textView.TextSnapshot;
                _resharperUtil.SetupGet(x => x.IsInstalled).Returns(true).Verifiable();
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcessAsCommand(KeyInputUtil.EnterKey));
                Assert.True(RunQueryStatus(KeyInputUtil.EnterKey));
                Assert.True(_bufferCoordinator.DiscardedKeyInput.IsNone());
                Assert.Equal(_textView.GetLine(0).Start, _textView.GetCaretPoint());
                Assert.Same(savedSnapshot, _textView.TextSnapshot);
                _factory.Verify();
            }
        }

        public sealed class ExecTest : VsCommandTargetTest
        {
            [Fact]
            public void PassOnIfCantHandle()
            {
                _vimBuffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
                Assert.False(_vimBuffer.CanProcess(VimKey.Enter));
                _nextTarget.SetupExecOne().Verifiable();
                RunExec(KeyInputUtil.EnterKey);
                _factory.Verify();
            }

            /// <summary>
            /// If a given KeyInput is marked for discarding make sure we don't pass it along to the
            /// next IOleCommandTarget.
            ///
            /// Also make sure that the Exec clears the discarded KeyInput
            /// </summary>
            [Fact]
            public void DiscardedKeyInput()
            {
                _bufferCoordinator.DiscardedKeyInput = FSharpOption.Create(KeyInputUtil.EscapeKey);
                RunExec(KeyInputUtil.EscapeKey);
                Assert.True(_bufferCoordinator.DiscardedKeyInput.IsNone());
                _factory.Verify();
            }

            /// <summary>
            /// The Exec method should clear out the discarded KeyInput value.  The discard is only
            /// meant to last for a single key stroke so the next Exec or QueryStatus should clear it 
            /// out.  
            /// 
            /// The clear of discard happens irrespective of what the current KeyInput is.  The point
            /// of DiscardedKeyInput is to prevent user input for a single key stroke.  If Exec comes
            /// along with a different KeyInput then clearly another KeyInput has happened and we 
            /// are done
            /// </summary>
            [Fact]
            public void ClearDiscardedKeyInput()
            {
                _bufferCoordinator.DiscardedKeyInput = FSharpOption.Create(KeyInputUtil.EnterKey);

                // Make sur Ecape isn't handled so it will go to the next IOleCommandTarget
                Assert.True(_vimBuffer.CanProcess(KeyInputUtil.EscapeKey));
                RunExec(KeyInputUtil.EscapeKey);
                Assert.True(_bufferCoordinator.DiscardedKeyInput.IsNone());
            }

            [Fact]
            public void HandleEscapeNormally()
            {
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                RunExec(KeyInputUtil.EscapeKey);
                Assert.Equal(1, count);
            }

            /// <summary>
            /// If there is buffered KeyInput values then the provided KeyInput shouldn't ever be 
            /// directly handled by the VsCommandTarget or the next IOleCommandTarget in the 
            /// chain.  It should be passed directly to the IVimBuffer if it can be handled else 
            /// it shouldn't be handled
            /// </summary>
            [Fact]
            public void WithUnmatchedBufferedInput()
            {
                _vim.KeyMap.MapWithNoRemap("jj", "hello", KeyRemapMode.Insert);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                RunExec('j');
                Assert.False(_vimBuffer.BufferedKeyInputs.IsEmpty);
                RunExec('a');
                Assert.Equal("ja", _textView.GetLine(0).GetText());
                Assert.True(_vimBuffer.BufferedKeyInputs.IsEmpty);
            }

            /// <summary>
            /// Make sure in the case that the next input matches the final expansion of a 
            /// buffered input that it's processed correctly
            /// </summary>
            [Fact]
            public void WithMatchedBufferedInput()
            {
                _vim.KeyMap.MapWithNoRemap("jj", "hello", KeyRemapMode.Insert);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                RunExec('j');
                Assert.False(_vimBuffer.BufferedKeyInputs.IsEmpty);
                RunExec('j');
                Assert.Equal("hello", _textView.GetLine(0).GetText());
                Assert.True(_vimBuffer.BufferedKeyInputs.IsEmpty);
            }

            /// <summary>
            /// In the case where there is buffered KeyInput values and the next KeyInput collapses
            /// it into a single value then we need to make sure we pass both values onto the IVimBuffer
            /// so the remapping can occur
            /// </summary>
            [Fact]
            public void CollapseBufferedInputToSingleKeyInput()
            {
                _vim.KeyMap.MapWithNoRemap("jj", "z", KeyRemapMode.Insert);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                RunExec('j');
                Assert.False(_vimBuffer.BufferedKeyInputs.IsEmpty);
                RunExec('j');
                Assert.Equal("z", _textView.GetLine(0).GetText());
                Assert.True(_vimBuffer.BufferedKeyInputs.IsEmpty);
            }

            /// <summary>
            /// If parameter info is up then the arrow keys should be routed to parameter info and
            /// not to the IVimBuffer
            /// </summary>
            [Fact]
            public void SignatureHelp_ArrowGoToCommandTarget()
            {
                _broker.SetupGet(x => x.IsSignatureHelpActive).Returns(true);

                var count = 0;
                _nextTarget.SetupExecOne().Callback(() => { count++; });

                foreach (var key in new[] { VimKey.Down, VimKey.Up })
                {
                    RunExec(VimKey.Down);
                }

                Assert.Equal(2, count);
            }

            /// <summary>
            /// Make sure the GoToDefinition command wil clear the active selection.  We don't want
            /// this command causing VsVim to switch to Visual Mode 
            /// </summary>
            [Fact]
            public void GoToDefinitionShouldClearSelection()
            {
                _textBuffer.SetText("dog", "cat");
                _textView.Selection.Select(_textBuffer.GetLineSpan(0, 3));
                _textManager.Setup(x => x.TextViews).Returns(new[] { _textView });
                _nextTarget.SetupExecAll();
                RunExec(CreateEditCommand(EditCommandKind.GoToDefinition));
                Assert.True(_textView.Selection.IsEmpty);
            }

            /// <summary>
            /// If the Comment command creates a selection then we should be turning it off 
            /// </summary>
            [Fact]
            public void CommentShouldClearSelection()
            {

            }
        }

        public sealed class ExecCoreTest : VsCommandTargetTest
        {
            /// <summary>
            /// Don't process GoToDefinition even if it has a KeyInput associated with it.  In the past
            /// a bug in OleCommandTarget.Convert created an EditCommand in this state.  That is a bug
            /// that should be fixed but also there is simply no reason that we should be processing this
            /// command here.  Let VS do the work
            /// </summary>
            [Fact]
            public void GoToDefinition()
            {
                var editCommand = CreateEditCommand(EditCommandKind.GoToDefinition);
                Action action = null;
                Assert.False(_targetRaw.ExecCore(editCommand, out action));
            }
        }
    }
}

