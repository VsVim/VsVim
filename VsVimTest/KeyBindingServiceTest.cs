using System;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using NUnit.Framework;
using VsVim;
using VsVim.Implementation;
using VsVim.UnitTest.Mock;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class KeyBindingServiceTest
    {
        private Mock<_DTE> _dte;
        private Mock<IOptionsDialogService> _optionsDialogService;
        private KeyBindingService _serviceRaw;
        private IKeyBindingService _service;
        private ILegacySettings _legacySettings;

        private void Create(params string[] args)
        {
            _dte = MockObjectFactory.CreateDteWithCommands(args);
            var sp = MockObjectFactory.CreateVsServiceProvider(
                Tuple.Create(typeof(SDTE), (object)(_dte.Object)),
                Tuple.Create(typeof(SVsShell), (object)(new Mock<IVsShell>(MockBehavior.Strict)).Object));
            _optionsDialogService = new Mock<IOptionsDialogService>(MockBehavior.Strict);
            _legacySettings = new VsVim.Settings.LegacySettings();
            _serviceRaw = new KeyBindingService(sp.Object, _optionsDialogService.Object, _legacySettings);
            _service = _serviceRaw;
        }

        private void Create()
        {
            Create("::ctrl+h", "::b");
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

        [Test, Description("Nothing should change since we haven't checked yet")]
        public void ResolveAnyConflicts1()
        {
            Create();
            Assert.AreEqual(ConflictingKeyBindingState.HasNotChecked, _serviceRaw.ConflictingKeyBindingState);
            _service.ResolveAnyConflicts();
            Assert.AreEqual(ConflictingKeyBindingState.HasNotChecked, _serviceRaw.ConflictingKeyBindingState);
        }

        [Test, Description("Nothing should change if they're ignored or resolved")]
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
    }
}
