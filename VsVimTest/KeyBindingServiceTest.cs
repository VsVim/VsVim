using VsVim;
using NUnit.Framework;
using System;
using EnvDTE;
using System.Collections.Generic;
using System.Linq;
using Vim;
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

        public static CommandKeyBinding CreateCommandKeyBinding(KeyInput input, string name = "again", string scope = "Global")
        {
            var key = new VsVim.KeyBinding(scope, input);
            return new CommandKeyBinding(name, key);
        }

        [Test()]
        public void FindConflictingCommands1()
        {
            var commands = Create("::ctrl+h");
            var inputs = new KeyInput[] { InputUtil.CharAndModifiersToKeyInput('h', KeyModifiers.Control) };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void FindConflictingCommands2()
        {
            var commands = Create("::h");
            var inputs = new KeyInput[] { new KeyInput('z') };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(0, list.Count);
        }

        [Test, Description("Conflicting key on first")]
        public void FindConflictingCommands3()
        {
            var commands = Create("::ctrl+z, h");
            var inputs = new KeyInput[] { InputUtil.CharAndModifiersToKeyInput('z', KeyModifiers.Control) };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        [Test, Description("Only check first key")]
        public void FindConflictingCommands4()
        {
            var commands = Create("::h, z");
            var inputs = new KeyInput[] { new KeyInput('z') };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void FindConflictingCommands5()
        {
            var commands = Create("::a","::ctrl+z, h");
            var inputs = new KeyInput[] { InputUtil.CharAndModifiersToKeyInput('z', KeyModifiers.Control) };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void FindConflictingCommands6()
        {
            var commands = Create("Global::ctrl+a", "Text Editor::ctrl+z");
            var inputs = new KeyInput[] { 
                InputUtil.CharAndModifiersToKeyInput('a', KeyModifiers.Control),
                InputUtil.CharAndModifiersToKeyInput('z', KeyModifiers.Control) };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(2, list.Count);
        }

        [Test]
        public void FindConflictingCommands7()
        {
            var commands = Create("balgh::a", "aoeu::z");
            var inputs = new KeyInput[] { new KeyInput('z'), new KeyInput('a') };
            var list = KeyBindingService.FindConflictingCommands(commands, new HashSet<KeyInput>(inputs));
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void IsImportantScope1()
        {
            Assert.IsTrue(KeyBindingService.IsImportantScope("Global"));
            Assert.IsTrue(KeyBindingService.IsImportantScope("Text Editor"));
            Assert.IsTrue(KeyBindingService.IsImportantScope(String.Empty));
        }

        [Test]
        public void IsImportantScope2()
        {
            Assert.IsFalse(KeyBindingService.IsImportantScope("blah"));
            Assert.IsFalse(KeyBindingService.IsImportantScope("VC Image Editor"));
        }

        [Test]
        public void ShouldSkip1()
        {
            var binding = CreateCommandKeyBinding(InputUtil.VimKeyToKeyInput(VimKey.LeftKey));
            Assert.IsTrue(KeyBindingService.ShouldSkip(binding));
        }
    }
}
