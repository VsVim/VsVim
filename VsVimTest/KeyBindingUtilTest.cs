using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using VsVim.Settings;
using VsVim.UnitTest.Mock;

namespace VsVim.UnitTest
{
    [TestFixture()]
    public class KeyBindingUtilTest
    {
        private static CommandKeyBinding CreateCommandKeyBinding(KeyInput input, KeyModifiers modifiers = KeyModifiers.None, string name = "again", string scope = "Global")
        {
            var stroke = new KeyStroke(input, modifiers);
            var key = new VsVim.KeyBinding(scope, stroke);
            return new CommandKeyBinding(name, key);
        }

        private static KeyBindingUtil Create(params string[] args)
        {
            var all = MockObjectFactory.CreateCommandList(args).Select(x => x.Object);
            var snapshot = new CommandsSnapshot(all);
            return new KeyBindingUtil(snapshot);
        }

        [Test()]
        public void FindConflictingCommands1()
        {
            var util = Create("::ctrl+h");
            var inputs = new KeyInput[] { KeyInputUtil.CharWithControlToKeyInput('h') };
            var list = util.FindConflictingCommandKeyBindings(new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void FindConflictingCommands2()
        {
            var util = Create("::h");
            var inputs = new KeyInput[] { KeyInputUtil.CharToKeyInput('z') };
            var list = util.FindConflictingCommandKeyBindings(new HashSet<KeyInput>(inputs));
            Assert.AreEqual(0, list.Count);
        }

        [Test, Description("Conflicting key on first")]
        public void FindConflictingCommands3()
        {
            var util = Create("::ctrl+z, h");
            var inputs = new KeyInput[] { KeyInputUtil.CharWithControlToKeyInput('z') };
            var list = util.FindConflictingCommandKeyBindings(new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        [Test, Description("Only check first key")]
        public void FindConflictingCommands4()
        {
            var util = Create("::h, z");
            var inputs = new KeyInput[] { KeyInputUtil.CharToKeyInput('z') };
            var list = util.FindConflictingCommandKeyBindings(new HashSet<KeyInput>(inputs));
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void FindConflictingCommands5()
        {
            var util = Create("::a", "::ctrl+z, h");
            var inputs = new KeyInput[] { KeyInputUtil.CharWithControlToKeyInput('z') };
            var list = util.FindConflictingCommandKeyBindings(new HashSet<KeyInput>(inputs));
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void FindConflictingCommands6()
        {
            var util = Create("Global::ctrl+a", "Text Editor::ctrl+z");
            var inputs = new KeyInput[] { 
                KeyInputUtil.CharWithControlToKeyInput('a'),
                KeyInputUtil.CharWithControlToKeyInput('z') };
            var list = util.FindConflictingCommandKeyBindings(new HashSet<KeyInput>(inputs));
            Assert.AreEqual(2, list.Count);
        }

        [Test]
        public void FindConflictingCommands7()
        {
            var util = Create("balgh::a", "aoeu::z");
            var inputs = new KeyInput[] { KeyInputUtil.CharToKeyInput('z'), KeyInputUtil.CharToKeyInput('a') };
            var list = util.FindConflictingCommandKeyBindings(new HashSet<KeyInput>(inputs));
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void IsImportantScope1()
        {
            Assert.IsTrue(KeyBindingUtil.IsImportantScope("Global"));
            Assert.IsTrue(KeyBindingUtil.IsImportantScope("Text Editor"));
            Assert.IsTrue(KeyBindingUtil.IsImportantScope(String.Empty));
        }

        [Test]
        public void IsImportantScope2()
        {
            Assert.IsFalse(KeyBindingUtil.IsImportantScope("blah"));
            Assert.IsFalse(KeyBindingUtil.IsImportantScope("VC Image Editor"));
        }

        [Test]
        public void ShouldSkip1()
        {
            var binding = CreateCommandKeyBinding(KeyInputUtil.VimKeyToKeyInput(VimKey.Left));
            Assert.IsTrue(KeyBindingUtil.ShouldSkip(binding));
        }

        [Test]
        public void FindRemovedKeyBindings1()
        {
            global::VsVim.Settings.Settings.Default.HaveUpdatedKeyBindings = true;
            global::VsVim.Settings.Settings.Default.RemovedBindings = new CommandBindingSetting[] {
                new CommandBindingSetting() { Name="foo", CommandString = "Scope::Ctrl+J" },
                new CommandBindingSetting() { Name="bar", CommandString = "Scope::Ctrl+J" } };
            var list = KeyBindingUtil.FindKeyBindingsMarkedAsRemoved();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("foo", list[0].Name);
            Assert.AreEqual("bar", list[1].Name);
        }
    }
}
