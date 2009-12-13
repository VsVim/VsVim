using VsVim;
using NUnit.Framework;
using System;
using EnvDTE;
using System.Collections.Generic;
using System.Linq;
using VimCore;
using Moq;
using System.Windows.Input;

namespace VsVimTest
{
    [TestFixture()]
    public class KeyBindingServiceTest
    {

        public IEnumerable<Command> Create(params string[] args)
        {
            foreach (var binding in args)
            {
                var localBinding = binding;
                var mock = new Mock<Command>(MockBehavior.Strict);
                mock.Setup(x => x.Bindings).Returns(localBinding);
                mock.Setup(x => x.Name).Returns("example command");
                mock.Setup(x => x.LocalizedName).Returns("example command");
                yield return mock.Object;
            }
        }

        [Test()]
        public void FindConflictingCommands1()
        {
            var commands = Create("::h");
            var inputs = new KeyInput[] { new KeyInput('h', Key.H) };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void FindConflictingCommands2()
        {
            var commands = Create("::h");
            var inputs = new KeyInput[] { new KeyInput('z', Key.Z) };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(0, list.Count);
        }

        [Test, Description("Conflicting key on first")]
        public void FindConflictingCommands3()
        {
            var commands = Create("::z, h");
            var inputs = new KeyInput[] { new KeyInput('z', Key.Z) };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        [Test, Description("Only check first key")]
        public void FindConflictingCommands4()
        {
            var commands = Create("::h, z");
            var inputs = new KeyInput[] { new KeyInput('z', Key.Z) };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void FindConflictingCommands5()
        {
            var commands = Create("::a","::z, h");
            var inputs = new KeyInput[] { new KeyInput('z', Key.Z) };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }
    }
}
