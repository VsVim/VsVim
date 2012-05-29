using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UI.Wpf;
using VsVim.Implementation;
using VsVim.UnitTest.Mock;

namespace VsVim.UnitTest
{
    [TestFixture]
    public sealed class KeyBindingServiceTest
    {
        private Mock<_DTE> _dte;
        private Mock<IOptionsDialogService> _optionsDialogService;
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
            _serviceRaw = new KeyBindingService(
                sp.Object, 
                _optionsDialogService.Object, 
                new Mock<IProtectedOperations>().Object,
                new Mock<ILegacySettings>().Object);
            _service = _serviceRaw;
        }

        private void Create()
        {
            Create("::ctrl+h", "::b");
        }

        private static CommandKeyBinding CreateCommandKeyBinding(KeyInput input, KeyModifiers modifiers = KeyModifiers.None, string name = "again", string scope = "Global")
        {
            var stroke = new KeyStroke(input, modifiers);
            var key = new VsVim.KeyBinding(scope, stroke);
            return new CommandKeyBinding(name, key);
        }

        [Test]
        public void Ctor1()
        {
            Create("::ctrl+h");
            Assert.AreEqual(ConflictingKeyBindingState.HasNotChecked, _service.ConflictingKeyBindingState);
        }

        [Test]
        public void IgnoreAnyConflicts1()
        {
            Create();
            _service.IgnoreAnyConflicts();
            Assert.AreEqual(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, _service.ConflictingKeyBindingState);
        }

        [Test]
        public void IgnoreAnyConflicts2()
        {
            Create();
            var didSee = false;
            _service.ConflictingKeyBindingStateChanged += (x, y) => { didSee = true; };
            _service.IgnoreAnyConflicts();
            Assert.IsTrue(didSee);
        }

        [Test]
        public void ResetConflictingKeyBindingState1()
        {
            Create();
            _service.IgnoreAnyConflicts();
            _service.ResetConflictingKeyBindingState();
            Assert.AreEqual(ConflictingKeyBindingState.HasNotChecked, _service.ConflictingKeyBindingState);
        }

        [Test]
        public void ResetConflictingKeyBindingState2()
        {
            Create();
            var didSee = false;
            _service.ConflictingKeyBindingStateChanged += (x, y) => { didSee = true; };
            _service.IgnoreAnyConflicts();
            _service.ResetConflictingKeyBindingState();
            Assert.IsTrue(didSee);
        }

        /// <summary>
        /// Nothing should change since we haven't checked yet
        /// </summary>
        [Test]
        public void ResolveAnyConflicts1()
        {
            Create();
            Assert.AreEqual(ConflictingKeyBindingState.HasNotChecked, _serviceRaw.ConflictingKeyBindingState);
            _service.ResolveAnyConflicts();
            Assert.AreEqual(ConflictingKeyBindingState.HasNotChecked, _serviceRaw.ConflictingKeyBindingState);
        }

        /// <summary>
        /// Nothing should change if they're ignored or resolved
        /// </summary>
        [Test]
        public void ResolveAnyConflicts2()
        {
            Create();
            _serviceRaw.UpdateConflictingState(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, null);
            _service.ResolveAnyConflicts();
            Assert.AreEqual(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, _serviceRaw.ConflictingKeyBindingState);
        }

        [Test]
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
            Assert.AreEqual(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, _service.ConflictingKeyBindingState);
        }

        [Test]
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
            Assert.AreEqual(ConflictingKeyBindingState.FoundConflicts, _service.ConflictingKeyBindingState);
        }

        [Test()]
        public void FindConflictingCommands1()
        {
            Create("::ctrl+h");
            var inputs = new KeyInput[] { KeyInputUtil.CharWithControlToKeyInput('h') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void FindConflictingCommands2()
        {
            Create("::h");
            var inputs = new KeyInput[] { KeyInputUtil.CharToKeyInput('z') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(0, list.Count);
        }

        /// <summary>
        /// Conflicting key on first
        /// </summary>
        [Test]
        public void FindConflictingCommands3()
        {
            Create("::ctrl+z, h");
            var inputs = new KeyInput[] { KeyInputUtil.CharWithControlToKeyInput('z') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        /// <summary>
        /// Only check first key
        /// </summary>
        [Test]
        public void FindConflictingCommands4()
        {
            Create("::h, z");
            var inputs = new KeyInput[] { KeyInputUtil.CharToKeyInput('z') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void FindConflictingCommands5()
        {
            Create("::a", "::ctrl+z, h");
            var inputs = new KeyInput[] { KeyInputUtil.CharWithControlToKeyInput('z') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void FindConflictingCommands6()
        {
            Create("Global::ctrl+a", "Text Editor::ctrl+z");
            var inputs = new KeyInput[] { 
                KeyInputUtil.CharWithControlToKeyInput('a'),
                KeyInputUtil.CharWithControlToKeyInput('z') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(2, list.Count);
        }

        [Test]
        public void FindConflictingCommands7()
        {
            Create("balgh::a", "aoeu::z");
            var inputs = new KeyInput[] { KeyInputUtil.CharToKeyInput('z'), KeyInputUtil.CharToKeyInput('a') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(0, list.Count);
        }

        /// <summary>
        /// In Vim ctlr+shift+f is exactly the same command as ctrl+f.  Vim simply ignores the 
        /// shift key when processing a control command with an alpha character.  Visual Studio
        /// though does differentiate.  Ctrl+f is differente than Ctrl+Shift+F.  So make sure
        /// we don't remove a Ctrl+Shift+F else find all will be disabled by default
        /// </summary>
        [Test]
        public void FindConflictingCommands_IgnoreControlPlusShift()
        {
            Create("::ctrl+shift+f", "::ctrl+f");
            var inputs = new[] { KeyInputUtil.CharWithControlToKeyInput('f') };
            var list = _serviceRaw.FindConflictingCommandKeyBindings(_commandsSnapshot, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void IsImportantScope1()
        {
            var set = KeyBindingService.GetDefaultImportantScopeSet();
            Assert.IsTrue(set.Contains("Global"));
            Assert.IsTrue(set.Contains("Text Editor"));
            Assert.IsTrue(set.Contains(String.Empty));
        }

        [Test]
        public void IsImportantScope2()
        {
           var set = KeyBindingService.GetDefaultImportantScopeSet();
            Assert.IsFalse(set.Contains("blah"));
            Assert.IsFalse(set.Contains("VC Image Editor"));
        }

        /// <summary>
        /// By default we should skip the unbinding of the arrow keys. They are too important 
        /// to VS experience and it nearly matches the Vim one anyways
        /// </summary>
        [Test]
        public void ShouldSkip_ArrowKeys()
        {
            var binding = CreateCommandKeyBinding(KeyInputUtil.VimKeyToKeyInput(VimKey.Left));
            Create();
            Assert.IsTrue(_serviceRaw.ShouldSkip(binding));
        }

        /// <summary>
        /// Don't skip function keys.  They are only used in Vim custom key bindings and hence
        /// it's something we really want to support if it's specified
        /// </summary>
        [Test]
        public void ShouldSkip_FunctionKeys()
        {
            var binding = CreateCommandKeyBinding(KeyInputUtil.VimKeyToKeyInput(VimKey.F2));
            Create();
            Assert.IsFalse(_serviceRaw.ShouldSkip(binding));
        }
    }
}
