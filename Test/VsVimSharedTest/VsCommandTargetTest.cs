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
using Vim.VisualStudio.Implementation;
using Vim.VisualStudio.Implementation.Misc;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using Vim.VisualStudio.Implementation.ReSharper;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class VsCommandTargetTest : VimTestBase
    {
        private readonly MockRepository _factory;
        private readonly IVimBuffer _vimBuffer;
        private readonly IVim _vim;
        private readonly ITextBuffer _textBuffer;
        private readonly ITextView _textView;
        private readonly Mock<IVsAdapter> _vsAdapter;
        private readonly Mock<IOleCommandTarget> _nextTarget;
        private readonly Mock<IDisplayWindowBroker> _broker;
        private readonly Mock<ITextManager> _textManager;
        private readonly Mock<IVimApplicationSettings> _vimApplicationSettings;
        private readonly IOleCommandTarget _target;
        private readonly VsCommandTarget _targetRaw;
        private readonly IVimBufferCoordinator _bufferCoordinator;

        protected VsCommandTargetTest(bool isReSharperInstalled)
        {
            _textView = CreateTextView("");
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _bufferCoordinator = new VimBufferCoordinator(_vimBuffer);
            _vim = _vimBuffer.Vim;
            _factory = new MockRepository(MockBehavior.Strict);

            _nextTarget = _factory.Create<IOleCommandTarget>(MockBehavior.Strict);
            _vsAdapter = _factory.Create<IVsAdapter>();
            _vsAdapter.SetupGet(x => x.KeyboardDevice).Returns(InputManager.Current.PrimaryKeyboardDevice);
            _vsAdapter.Setup(x => x.InAutomationFunction).Returns(false);
            _vsAdapter.Setup(x => x.InDebugMode).Returns(false);
            _vsAdapter.Setup(x => x.IsIncrementalSearchActive(It.IsAny<ITextView>())).Returns(false);

            _broker = _factory.Create<IDisplayWindowBroker>(MockBehavior.Loose);
            _textManager = _factory.Create<ITextManager>();
            _vimApplicationSettings = _factory.Create<IVimApplicationSettings>();

            var commandTargets = new List<ICommandTarget>();
            if (isReSharperInstalled)
            {
                commandTargets.Add(ReSharperKeyUtil.GetOrCreate(_bufferCoordinator));
            }
            commandTargets.Add(new StandardCommandTarget(_bufferCoordinator, _textManager.Object, _broker.Object, _nextTarget.Object));

            var oldCommandFilter = _nextTarget.Object;
            _targetRaw = new VsCommandTarget(
                _bufferCoordinator,
                _textManager.Object,
                _vsAdapter.Object,
                _broker.Object,
                KeyUtil,
                _vimApplicationSettings.Object,
                _nextTarget.Object,
                commandTargets.ToReadOnlyCollectionShallow());
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

        public sealed class TryCustomProcessTest : VsCommandTargetTest
        {
            public TryCustomProcessTest()
                : base(isReSharperInstalled: false)
            {
                _vimBuffer.LocalSettings.SoftTabStop = 4;
                _vimApplicationSettings.SetupGet(x => x.CleanMacros).Returns(false);
            }

            [Fact]
            public void BackNoSoftTabStop()
            {
                _nextTarget.SetupExecOne().Verifiable();
                _vimApplicationSettings.SetupGet(x => x.UseEditorTabAndBackspace).Returns(true);
                Assert.True(_targetRaw.TryCustomProcess(InsertCommand.Back));
                _factory.Verify();
            }

            /// <summary>
            /// Don't custom process back when 'sts' is enabled, let Vim handle it
            /// </summary>
            [Fact]
            public void BackSoftTabStop()
            {
                _vimApplicationSettings.SetupGet(x => x.UseEditorTabAndBackspace).Returns(false);
                Assert.False(_targetRaw.TryCustomProcess(InsertCommand.Back));
            }

            [Fact]
            public void TabNoSoftTabStop()
            {
                _vimApplicationSettings.SetupGet(x => x.UseEditorTabAndBackspace).Returns(true);
                _nextTarget.SetupExecOne().Verifiable();
                Assert.True(_targetRaw.TryCustomProcess(InsertCommand.InsertTab));
                _factory.Verify();
            }

            /// <summary>
            /// Don't custom process tab when 'sts' is enabled, let Vim handle it
            /// </summary>
            [Fact]
            public void TabSoftTabStop()
            {
                _vimApplicationSettings.SetupGet(x => x.UseEditorTabAndBackspace).Returns(false);
                _vimBuffer.LocalSettings.SoftTabStop = 4;
                Assert.False(_targetRaw.TryCustomProcess(InsertCommand.InsertTab));
            }

            /// <summary>
            /// Don't custom process anything when doing clean macro recording.  Let core vim
            /// handle it all so we don't affect the output with intellisense.
            /// </summary>
            [Fact]
            public void CleanMacrosRecording()
            {
                try
                {
                    _vim.MacroRecorder.StartRecording(UnnamedRegister, isAppend: false);
                    _vimApplicationSettings.SetupGet(x => x.CleanMacros).Returns(true);
                    _vimApplicationSettings.SetupGet(x => x.UseEditorTabAndBackspace).Returns(false);
                    Assert.False(_targetRaw.TryCustomProcess(InsertCommand.InsertTab));
                    Assert.False(_targetRaw.TryCustomProcess(InsertCommand.Back));
                }
                finally
                {
                    _vim.MacroRecorder.StopRecording();
                }
            }

            [Fact]
            public void CleanMacrosNotRecording()
            {
                _nextTarget.SetupExecOne();
                _vimApplicationSettings.SetupGet(x => x.CleanMacros).Returns(true);
                _vimApplicationSettings.SetupGet(x => x.UseEditorTabAndBackspace).Returns(true);
                Assert.False(_vim.MacroRecorder.IsRecording);
                Assert.True(_targetRaw.TryCustomProcess(InsertCommand.InsertTab));
                Assert.True(_targetRaw.TryCustomProcess(InsertCommand.Back));
            }
        }

        public sealed class TryConvertTest : VsCommandTargetTest
        {
            public TryConvertTest()
                : base(isReSharperInstalled: false)
            {
            }

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

            [Fact(Skip = "https://github.com/jaredpar/VsVim/issues/1863")]
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
            public QueryStatusTest()
                : base(isReSharperInstalled: false)
            {
            }

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
        }

        public sealed class ExecTest : VsCommandTargetTest
        {
            public ExecTest()
                : base(isReSharperInstalled: false)
            {
            }

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
            /// </summary>
            [Fact(Skip = "https://github.com/jaredpar/VsVim/issues/1863")]
            public void DiscardedKeyInput()
            {
                _bufferCoordinator.Discard(KeyInputUtil.EscapeKey);
                RunExec(KeyInputUtil.EscapeKey);
                _factory.Verify();
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
                _textManager.Setup(x => x.GetDocumentTextViews(DocumentLoad.RespectLazy)).Returns(new[] { _textView });
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
            public ExecCoreTest()
                : base(isReSharperInstalled: false)
            {
            }

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
                Assert.False(_targetRaw.Exec(editCommand, out action));
            }
        }

        public sealed class ReSharperQueryStatusTest : VsCommandTargetTest
        {
            public ReSharperQueryStatusTest()
                : base(isReSharperInstalled: true)
            {
            }

            /// Don't actually run the Escape in the QueryStatus command if we're in visual mode
            /// </summary>
            [Fact]
            public void EnableEscapeAndDontHandleInResharperPlusVisualMode()
            {
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None);
                RunQueryStatus(KeyInputUtil.EscapeKey);
                Assert.Equal(0, count);
            }

            /// <summary>
            /// Make sure we process Escape during QueryStatus if we're in insert mode and still pass
            /// it on to R#.  R# will intercept escape and never give it to us and we'll think 
            /// we're still in insert.  
            /// </summary>
            [Fact(Skip = "https://github.com/jaredpar/VsVim/issues/1863")]
            public void EnableAndHandleEscape()
            {
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                Assert.True(RunQueryStatus(KeyInputUtil.EscapeKey));
                Assert.True(_bufferCoordinator.IsDiscarded(KeyInputUtil.EscapeKey));
                Assert.Equal(1, count);
            }

            /// <summary>
            /// When Back is processed as a command make sure we handle it in QueryStatus and hide
            /// it from R#.  Back in R# is used to do special parens delete and we don't want that
            /// overriding a command
            /// </summary>
            [Fact]
            public void BackspaceAsCommand()
            {
                var backKeyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Back);
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcessAsCommand(backKeyInput));
                Assert.False(RunQueryStatus(backKeyInput));
                Assert.True(_bufferCoordinator.IsDiscarded(backKeyInput));
                Assert.Equal(1, count);
            }

            /// <summary>
            /// When Back is processed as an edit make sure we don't special case it and instead let
            /// it go back to R# for processing.  They special case Back during edit to do actions
            /// like matched paren deletion that we want to enable.
            /// </summary>
            [Fact]
            public void BackspaceInInsert()
            {
                var backKeyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Back);
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcessAsCommand(backKeyInput));
                Assert.True(RunQueryStatus(backKeyInput));
                Assert.False(_bufferCoordinator.HasDiscardedKeyInput);
                Assert.Equal(0, count);
            }

            /// <summary>
            /// Make sure we process Escape during QueryStatus if we're in insert mode.  R# will
            /// intercept escape and never give it to us and we'll think we're still in insert
            /// </summary>
            [Fact(Skip = "https://github.com/jaredpar/VsVim/issues/1863")]
            public void EnableAndHandleEscapeInResharperPlusExternalEdit()
            {
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.ExternalEdit, ModeArgument.None);
                Assert.True(RunQueryStatus(KeyInputUtil.EscapeKey));
                Assert.True(_bufferCoordinator.IsDiscarded(KeyInputUtil.EscapeKey));
                Assert.Equal(1, count);
            }

            /// <summary>
            /// The PageUp key isn't special so don't special case it in R#
            /// </summary>
            [Fact]
            public void HandlePageUpNormally()
            {
                var count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(RunQueryStatus(KeyInputUtil.VimKeyToKeyInput(VimKey.PageUp)));
                Assert.Equal(0, count);
            }

            /// <summary>
            /// When Visual Studio is in debug mode R# will attempt to handle the Enter key directly
            /// and do nothing.  Presumably they are doing this because it is an edit command and they
            /// are suppressing it's action.  We want to process this directly though if Vim believes
            /// Enter to be a command and not an edit, for example in normal mode
            /// </summary>
            [Fact(Skip = "https://github.com/jaredpar/VsVim/issues/1863")]
            public void EnterAsCommand()
            {
                _textView.SetText("cat", "dog");
                _textView.MoveCaretTo(0);
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcessAsCommand(KeyInputUtil.EnterKey));
                Assert.False(RunQueryStatus(KeyInputUtil.EnterKey));
                Assert.Equal(_textView.GetLine(1).Start, _textView.GetCaretPoint());
                Assert.True(_bufferCoordinator.IsDiscarded(KeyInputUtil.EnterKey));
            }

            /// <summary>
            /// If Enter isn't going to be processed as a command then don't special case it
            /// mode for R#.  It would be an edit and we don't want to interfere with R#'s handling 
            /// of edits
            /// </summary>
            [Fact]
            public void EnterInInsert()
            {
                _textView.SetText("cat", "dog");
                _textView.MoveCaretTo(0);
                var savedSnapshot = _textView.TextSnapshot;
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                Assert.True(_vimBuffer.CanProcessAsCommand(KeyInputUtil.EnterKey));
                Assert.True(RunQueryStatus(KeyInputUtil.EnterKey));
                Assert.False(_bufferCoordinator.HasDiscardedKeyInput);
                Assert.Equal(_textView.GetLine(0).Start, _textView.GetCaretPoint());
                Assert.Same(savedSnapshot, _textView.TextSnapshot);
            }
        }
    }
}

