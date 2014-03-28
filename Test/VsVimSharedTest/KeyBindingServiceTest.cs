using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using Xunit;
using Vim;
using Vim.UI.Wpf;
using VsVim.Implementation;
using VsVim.UnitTest.Mock;
using Vim.UnitTest;
using VsVim.Implementation.Misc;
using EditorUtils;
using Microsoft.VisualStudio;

namespace VsVim.UnitTest
{
    public abstract class KeyBindingServiceTest : VimTestBase
    {
        private Mock<_DTE> _dte;
        private Mock<IKeyboardOptionsProvider> _keyboardOptionsProvider;
        private Mock<IVimApplicationSettings> _vimApplicationSettings;
        private KeyBindingService _serviceRaw;
        private IKeyBindingService _service;
        private CommandListSnapshot _commandListSnapshot;

        private void Create(params string[] args)
        {
            Create(MockObjectFactory.CreateCommandList(args).Select(x => x.Object).ToArray());
        }

        protected virtual void Create(params EnvDTE.Command[] args)
        {
            _dte = MockObjectFactory.CreateDteWithCommands(args);
            _commandListSnapshot = new CommandListSnapshot(_dte.Object);
            _keyboardOptionsProvider = new Mock<IKeyboardOptionsProvider>(MockBehavior.Strict);
            _vimApplicationSettings = new Mock<IVimApplicationSettings>(MockBehavior.Strict);
            _vimApplicationSettings.SetupProperty(x => x.IgnoredConflictingKeyBinding, false);
            _vimApplicationSettings.SetupProperty(x => x.HaveUpdatedKeyBindings, false);
            _vimApplicationSettings.SetupProperty(x => x.KeyMappingIssueFixed, true);

            var list = new List<CommandKeyBinding>();
            _vimApplicationSettings.SetupGet(x => x.RemovedBindings).Returns(list.AsReadOnly());

            _serviceRaw = new KeyBindingService(
                _dte.Object,
                _keyboardOptionsProvider.Object,
                new Mock<IVimProtectedOperations>().Object,
                _vimApplicationSettings.Object,
                ScopeData.Default);
            _service = _serviceRaw;

            var result = _dte.Object.Commands.Count;
        }

        private void Create()
        {
            Create("::ctrl+h", "::b");
        }

        public sealed class FixKeyMappingTest : KeyBindingServiceTest
        {
            protected override void Create(params EnvDTE.Command[] args)
            {
                base.Create(args);
                _vimApplicationSettings.Object.KeyMappingIssueFixed = false;
            }

            [Fact]
            public void FixEnter()
            {
                var enterCommand = MockObjectFactory.CreateCommand(
                    VSConstants.VSStd2K,
                    (int)VSConstants.VSStd2KCmdID.RETURN,
                    "");
                Create(enterCommand.Object);
                _serviceRaw.FixKeyMappingIssue();
                Assert.Equal(new[] { "Global::Enter" }, enterCommand.Object.GetBindings());
                Assert.True(_vimApplicationSettings.Object.KeyMappingIssueFixed);
            }

            [Fact]
            public void FixBackspace()
            {
                var enterCommand = MockObjectFactory.CreateCommand(
                    VSConstants.VSStd2K,
                    (int)VSConstants.VSStd2KCmdID.BACKSPACE,
                    "");
                Create(enterCommand.Object);
                _serviceRaw.FixKeyMappingIssue();
                Assert.Equal(new[] { "Global::Bkspce" }, enterCommand.Object.GetBindings());
                Assert.True(_vimApplicationSettings.Object.KeyMappingIssueFixed);
            }
        }

        public sealed class MiscTest : KeyBindingServiceTest
        {
            private static CommandKeyBinding CreateCommandKeyBinding(KeyInput input, KeyModifiers modifiers = KeyModifiers.None, string name = "again", string scope = "Global")
            {
                var stroke = new KeyStroke(input, modifiers);
                var key = new VsVim.KeyBinding(scope, stroke);
                return new CommandKeyBinding(new CommandId(), name, key);
            }

            [Fact]
            public void Ctor1()
            {
                Create("::ctrl+h");
                Assert.Equal(ConflictingKeyBindingState.HasNotChecked, _service.ConflictingKeyBindingState);
            }

            [Fact]
            public void IgnoreAnyConflicts1()
            {
                Create();
                _service.IgnoreAnyConflicts();
                Assert.Equal(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, _service.ConflictingKeyBindingState);
            }

            [Fact]
            public void IgnoreAnyConflicts2()
            {
                Create();
                var didSee = false;
                _service.ConflictingKeyBindingStateChanged += (x, y) => { didSee = true; };
                _service.IgnoreAnyConflicts();
                Assert.True(didSee);
            }

            [Fact]
            public void ResetConflictingKeyBindingState1()
            {
                Create();
                _service.IgnoreAnyConflicts();
                _service.ResetConflictingKeyBindingState();
                Assert.Equal(ConflictingKeyBindingState.HasNotChecked, _service.ConflictingKeyBindingState);
            }

            [Fact]
            public void ResetConflictingKeyBindingState2()
            {
                Create();
                var didSee = false;
                _service.ConflictingKeyBindingStateChanged += (x, y) => { didSee = true; };
                _service.IgnoreAnyConflicts();
                _service.ResetConflictingKeyBindingState();
                Assert.True(didSee);
            }

            /// <summary>
            /// Nothing should change since we haven't checked yet
            /// </summary>
            [Fact]
            public void ResolveAnyConflicts1()
            {
                Create();
                Assert.Equal(ConflictingKeyBindingState.HasNotChecked, _serviceRaw.ConflictingKeyBindingState);
                _service.ResolveAnyConflicts();
                Assert.Equal(ConflictingKeyBindingState.HasNotChecked, _serviceRaw.ConflictingKeyBindingState);
            }

            /// <summary>
            /// Nothing should change if they're ignored or resolved
            /// </summary>
            [Fact]
            public void ResolveAnyConflicts2()
            {
                Create();
                _serviceRaw.ConflictingKeyBindingState = ConflictingKeyBindingState.ConflictsIgnoredOrResolved;
                _service.ResolveAnyConflicts();
                Assert.Equal(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, _serviceRaw.ConflictingKeyBindingState);
            }

            [Fact]
            public void ResolveAnyConflicts3()
            {
                Create("::ctrl+h");
                _serviceRaw.ConflictingKeyBindingState = ConflictingKeyBindingState.FoundConflicts;
                _serviceRaw.VimFirstKeyInputSet = new HashSet<KeyInput>();
                _keyboardOptionsProvider.Setup(x => x.ShowOptionsPage()).Verifiable();
                _serviceRaw.ResolveAnyConflicts();
                Assert.Equal(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, _service.ConflictingKeyBindingState);
            }

            [Fact]
            public void FindConflictingCommands1()
            {
                Create("::ctrl+h");
                var inputs = new KeyInput[] { KeyInputUtil.CharWithControlToKeyInput('h') };
                var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandListSnapshot, new HashSet<KeyInput>(inputs));
                Assert.Equal(1, list.Count);
            }

            [Fact]
            public void FindConflictingCommands2()
            {
                Create("::h");
                var inputs = new KeyInput[] { KeyInputUtil.CharToKeyInput('z') };
                var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandListSnapshot, new HashSet<KeyInput>(inputs));
                Assert.Equal(0, list.Count);
            }

            /// <summary>
            /// Conflicting key on first
            /// </summary>
            [Fact]
            public void FindConflictingCommands3()
            {
                Create("::ctrl+z, h");
                var inputs = new KeyInput[] { KeyInputUtil.CharWithControlToKeyInput('z') };
                var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandListSnapshot, new HashSet<KeyInput>(inputs));
                Assert.Equal(1, list.Count);
            }

            /// <summary>
            /// Only check first key
            /// </summary>
            [Fact]
            public void FindConflictingCommands4()
            {
                Create("::h, z");
                var inputs = new KeyInput[] { KeyInputUtil.CharToKeyInput('z') };
                var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandListSnapshot, new HashSet<KeyInput>(inputs));
                Assert.Equal(0, list.Count);
            }

            [Fact]
            public void FindConflictingCommands5()
            {
                Create("::a", "::ctrl+z, h");
                var inputs = new KeyInput[] { KeyInputUtil.CharWithControlToKeyInput('z') };
                var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandListSnapshot, new HashSet<KeyInput>(inputs));
                Assert.Equal(1, list.Count);
            }

            [Fact]
            public void FindConflictingCommands6()
            {
                Create("Global::ctrl+a", "Text Editor::ctrl+z");
                var inputs = new KeyInput[] { 
                KeyInputUtil.CharWithControlToKeyInput('a'),
                KeyInputUtil.CharWithControlToKeyInput('z') };
                var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandListSnapshot, new HashSet<KeyInput>(inputs));
                Assert.Equal(2, list.Count);
            }

            [Fact]
            public void FindConflictingCommands7()
            {
                Create("balgh::a", "aoeu::z");
                var inputs = new KeyInput[] { KeyInputUtil.CharToKeyInput('z'), KeyInputUtil.CharToKeyInput('a') };
                var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandListSnapshot, new HashSet<KeyInput>(inputs));
                Assert.Equal(0, list.Count);
            }

            /// <summary>
            /// In Vim ctlr+shift+f is exactly the same command as ctrl+f.  Vim simply ignores the 
            /// shift key when processing a control command with an alpha character.  Visual Studio
            /// though does differentiate.  Ctrl+f is differente than Ctrl+Shift+F.  So make sure
            /// we don't remove a Ctrl+Shift+F else find all will be disabled by default
            /// </summary>
            [Fact]
            public void FindConflictingCommands_IgnoreControlPlusShift()
            {
                Create("::ctrl+shift+f", "::ctrl+f");
                var inputs = new[] { KeyInputUtil.CharWithControlToKeyInput('f') };
                var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandListSnapshot, new HashSet<KeyInput>(inputs));
                Assert.Equal(1, list.Count);
            }

            /// <summary>
            /// By default we should skip the unbinding of the arrow keys. They are too important 
            /// to VS experience and it nearly matches the Vim one anyways
            /// </summary>
            [Fact]
            public void ShouldSkip_ArrowKeys()
            {
                var binding = CreateCommandKeyBinding(KeyInputUtil.VimKeyToKeyInput(VimKey.Left));
                Create();
                Assert.True(_serviceRaw.ShouldSkip(binding));
            }

            /// <summary>
            /// Don't skip function keys.  They are only used in Vim custom key bindings and hence
            /// it's something we really want to support if it's specified
            /// </summary>
            [Fact]
            public void ShouldSkip_FunctionKeys()
            {
                var binding = CreateCommandKeyBinding(KeyInputUtil.VimKeyToKeyInput(VimKey.F2));
                Create();
                Assert.False(_serviceRaw.ShouldSkip(binding));
            }

            /// <summary>
            /// Make sure that after running the conflicting check and there are conflicts that we
            /// store the information
            /// </summary>
            [Fact]
            public void RunConflictingKeyBindingStateCheck_SetSnapshot()
            {
                Create("::d", "::ctrl+h", "::b");
                var vimBuffer = CreateVimBuffer("");
                _service.RunConflictingKeyBindingStateCheck(vimBuffer);
                Assert.NotNull(_serviceRaw.ConflictingKeyBindingState);
                Assert.Equal(ConflictingKeyBindingState.FoundConflicts, _serviceRaw.ConflictingKeyBindingState);
            }

            /// <summary>
            /// Make sure we correctly detect there are no conflicts if there are none
            /// </summary>
            [Fact]
            public void RunConflictingKeyBindingStateCheck_NoConflicts()
            {
                Create(new string[] { });
                var vimBuffer = CreateVimBuffer("");
                _service.RunConflictingKeyBindingStateCheck(vimBuffer);
                Assert.Equal(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, _serviceRaw.ConflictingKeyBindingState);
            }
        }
    }
}
