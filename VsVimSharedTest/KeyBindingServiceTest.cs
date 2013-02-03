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

namespace VsVim.UnitTest
{
    public sealed class KeyBindingServiceTest : VimTestBase
    {
        private Mock<_DTE> _dte;
        private Mock<IOptionsDialogService> _optionsDialogService;
        private Mock<IVimApplicationSettings> _vimApplicationSettings;
        private KeyBindingService _serviceRaw;
        private IKeyBindingService _service;
        private CommandsSnapshot _commandsSnapshot;

        private void Create(params string[] args)
        {
            _dte = MockObjectFactory.CreateDteWithCommands(args);
            _commandsSnapshot = new CommandsSnapshot(_dte.Object);
            var sp = MockObjectFactory.CreateVsServiceProvider(
                Tuple.Create(typeof(SDTE), (object)(_dte.Object)),
                Tuple.Create(typeof(SVsShell), (object)(new Mock<IVsShell>(MockBehavior.Strict)).Object));
            _optionsDialogService = new Mock<IOptionsDialogService>(MockBehavior.Strict);
            _vimApplicationSettings = new Mock<IVimApplicationSettings>(MockBehavior.Strict);
            _vimApplicationSettings.SetupGet(x => x.IgnoredConflictingKeyBinding).Returns(false);
            _vimApplicationSettings.SetupGet(x => x.HaveUpdatedKeyBindings).Returns(false);

            var list = new List<CommandKeyBinding>();
            _vimApplicationSettings.SetupGet(x => x.RemovedBindings).Returns(list.AsReadOnly());

            _serviceRaw = new KeyBindingService(
                sp.Object, 
                _optionsDialogService.Object, 
                new Mock<IProtectedOperations>().Object,
                _vimApplicationSettings.Object);
            _service = _serviceRaw;

            var result = _dte.Object.Commands.Count;
        }

        private void Create()
        {
            Create("::ctrl+h", "::b");
        }

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
            _serviceRaw.UpdateConflictingState(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, null);
            _service.ResolveAnyConflicts();
            Assert.Equal(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, _serviceRaw.ConflictingKeyBindingState);
        }

        [Fact]
        public void ResolveAnyConflicts3()
        {
            Create("::ctrl+h");
            var snapshot = new CommandKeyBindingSnapshot(
                new CommandsSnapshot(_dte.Object),
                Enumerable.Empty<CommandKeyBinding>(),
                Enumerable.Empty<CommandKeyBinding>());
            _serviceRaw.UpdateConflictingState(ConflictingKeyBindingState.FoundConflicts, snapshot);
            _optionsDialogService.Setup(x => x.ShowConflictingKeyBindingsDialog(snapshot)).Returns(true).Verifiable();
            _serviceRaw.ResolveAnyConflicts();
            Assert.Equal(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, _service.ConflictingKeyBindingState);
        }

        [Fact]
        public void ResolveAnyConflicts4()
        {
            Create("::ctrl+h");
            var snapshot = new CommandKeyBindingSnapshot(
                new CommandsSnapshot(_dte.Object),
                Enumerable.Empty<CommandKeyBinding>(),
                Enumerable.Empty<CommandKeyBinding>());
            _serviceRaw.UpdateConflictingState(ConflictingKeyBindingState.FoundConflicts, snapshot);
            _optionsDialogService.Setup(x => x.ShowConflictingKeyBindingsDialog(snapshot)).Returns(false).Verifiable();
            _serviceRaw.ResolveAnyConflicts();
            Assert.Equal(ConflictingKeyBindingState.FoundConflicts, _service.ConflictingKeyBindingState);
        }

        [Fact]
        public void FindConflictingCommands1()
        {
            Create("::ctrl+h");
            var inputs = new KeyInput[] { KeyInputUtil.CharWithControlToKeyInput('h') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.Equal(1, list.Count);
        }

        [Fact]
        public void FindConflictingCommands2()
        {
            Create("::h");
            var inputs = new KeyInput[] { KeyInputUtil.CharToKeyInput('z') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
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
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
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
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.Equal(0, list.Count);
        }

        [Fact]
        public void FindConflictingCommands5()
        {
            Create("::a", "::ctrl+z, h");
            var inputs = new KeyInput[] { KeyInputUtil.CharWithControlToKeyInput('z') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.Equal(1, list.Count);
        }

        [Fact]
        public void FindConflictingCommands6()
        {
            Create("Global::ctrl+a", "Text Editor::ctrl+z");
            var inputs = new KeyInput[] { 
                KeyInputUtil.CharWithControlToKeyInput('a'),
                KeyInputUtil.CharWithControlToKeyInput('z') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public void FindConflictingCommands7()
        {
            Create("balgh::a", "aoeu::z");
            var inputs = new KeyInput[] { KeyInputUtil.CharToKeyInput('z'), KeyInputUtil.CharToKeyInput('a') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
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
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.Equal(1, list.Count);
        }

        [Fact]
        public void IsImportantScope1()
        {
            var set = KeyBindingService.CreateDefaultImportantScopeSet();
            Assert.True(set.Contains("Global"));
            Assert.True(set.Contains("Text Editor"));
            Assert.True(set.Contains(String.Empty));
        }

        [Fact]
        public void IsImportantScope2()
        {
            var set = KeyBindingService.CreateDefaultImportantScopeSet();
            Assert.False(set.Contains("blah"));
            Assert.False(set.Contains("VC Image Editor"));
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
