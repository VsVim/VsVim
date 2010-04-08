using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VsVim.Implementation;
using Microsoft.VisualStudio.Shell.Interop;
using VsVim;

namespace VsVimTest
{
    [TestFixture]
    public class KeyBindingServiceTest
    {
        private KeyBindingService Create(params string[] args)
        {
            var dte = MockObjectFactory.CreateDteWithCommands(args);
            var sp = MockObjectFactory.CreateVsServiceProvider(Tuple.Create(typeof(SDTE), (object)(dte.Object)));
            return new KeyBindingService(sp.Object);
        }

        private KeyBindingService Create()
        {
            return Create("::ctrl+h", "::b");
        }

        [Test]
        public void Ctor1()
        {
            var service = Create("::ctrl+h");
            Assert.AreEqual(ConflictingKeyBindingState.HasNotChecked, service.ConflictingKeyBindingState);
        }

        [Test]
        public void IgnoreAnyConflicts1()
        {
            var service = Create();
            service.IgnoreAnyConflicts();
            Assert.AreEqual(ConflictingKeyBindingState.ConflictsIgnoredOrResolved, service.ConflictingKeyBindingState);
        }

        [Test]
        public void IgnoreAnyConflicts2()
        {
            var service = Create();
            var didSee = false;
            service.ConflictingKeyBindingStateChanged += (x, y) => { didSee = true; };
            service.IgnoreAnyConflicts();
            Assert.IsTrue(didSee);
        }

        [Test]
        public void ResetConflictingKeyBindingState1()
        {
            var service = Create();
            service.IgnoreAnyConflicts();
            service.ResetConflictingKeyBindingState();
            Assert.AreEqual(ConflictingKeyBindingState.HasNotChecked, service.ConflictingKeyBindingState);
        }

        [Test]
        public void ResetConflictingKeyBindingState2()
        {
            var service = Create();
            var didSee = false;
            service.ConflictingKeyBindingStateChanged += (x, y) => { didSee = true; };
            service.IgnoreAnyConflicts();
            service.ResetConflictingKeyBindingState();
            Assert.IsTrue(didSee);
        }
    }
}
