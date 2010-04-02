using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VsVim;
using System.Windows.Input;
using KeyBinding = VsVim.KeyBinding;
using Vim;

namespace VsVimTest
{
    [TestFixture]
    public class KeyBindingTest
    {

        [Test]
        public void Parse1()
        {
            var b = KeyBinding.Parse("foo::e");
            Assert.AreEqual("foo", b.Scope);
            Assert.AreEqual('e', b.FirstKeyInput.Char);
        }

        [Test]
        public void Parse2()
        {
            var b = KeyBinding.Parse("::b");
            Assert.AreEqual(String.Empty, b.Scope);
            Assert.AreEqual('b', b.FirstKeyInput.Char);
        }

        [Test]
        public void Parse3()
        {
            var b = KeyBinding.Parse("::f2");
            Assert.AreEqual(Char.MinValue, b.FirstKeyInput.Char);
            Assert.AreEqual(VimKey.F2Key, b.FirstKeyInput.Key);
        }

        [Test, Description("Parse a keybinding with , correctly")]
        public void Parse4()
        {
            var b = KeyBinding.Parse("::,");
            Assert.AreEqual(',', b.FirstKeyInput.Char);
            Assert.AreEqual(KeyModifiers.None, b.FirstKeyInput.KeyModifiers);
        }

        [Test, Description("Double modifier")]
        public void Parse5()
        {
            var b = KeyBinding.Parse("::ctrl+shift+f");
            Assert.AreEqual('f', b.FirstKeyInput.Char);
            Assert.IsTrue(0 != (KeyModifiers.Shift & b.FirstKeyInput.KeyModifiers));
            Assert.IsTrue(0 != (KeyModifiers.Control & b.FirstKeyInput.KeyModifiers));
        }

        [Test, Description("Don't carry shift keys for letters")]
        public void Parse6()
        {
            var b = KeyBinding.Parse("::ctrl+D");
            Assert.AreEqual('d', b.FirstKeyInput.Char);
            Assert.AreEqual(KeyModifiers.Control, b.FirstKeyInput.KeyModifiers);
        }

        [Test]
        public void ParseMultiple1()
        {
            var b = KeyBinding.Parse("::e, f");
            Assert.AreEqual(2, b.KeyInputs.Count());
        }

        [Test, Description("With a comma key")]
        public void ParseMultiple2()
        {
            var b = KeyBinding.Parse("::,, f");
            Assert.AreEqual(2, b.KeyInputs.Count());
            Assert.AreEqual(',', b.KeyInputs.ElementAt(0).Char);
            Assert.AreEqual('f', b.KeyInputs.ElementAt(1).Char);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void BadParse1()
        {
            KeyBinding.Parse("::notavalidkey");
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void BadParse2()
        {
            KeyBinding.Parse("::ctrl+notavalidkey");
        }

        [Test, ExpectedException(typeof(ArgumentException)), Description("Not supported because simply put I don't understand it")]
        public void BadParse3()
        {
            KeyBinding.Parse("::Num *");
        }

        [Test]
        public void VsKeyBackSpace()
        {
            var b = KeyBinding.Parse("::Bkspce");
            Assert.AreEqual(VimKey.BackKey, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyLeftArrow()
        {
            var b = KeyBinding.Parse("::Left Arrow");
            Assert.AreEqual(VimKey.LeftKey, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyRightArrow()
        {
            var b = KeyBinding.Parse("::Right Arrow");
            Assert.AreEqual(VimKey.RightKey, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyUpArrow()
        {
            var b = KeyBinding.Parse("::Up Arrow");
            Assert.AreEqual(VimKey.UpKey, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyDownArrow()
        {
            var b = KeyBinding.Parse("::Down Arrow");
            Assert.AreEqual(VimKey.DownKey, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyDownArrow2()
        {
            var b = KeyBinding.Parse("::Shift+Down Arrow");
            Assert.AreEqual(VimKey.DownKey, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyPageDown()
        {
            var b = KeyBinding.Parse("::PgDn");
            Assert.AreEqual(VimKey.PageDownKey, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyPageUp()
        {
            var b = KeyBinding.Parse("::PgUp");
            Assert.AreEqual(VimKey.PageUpKey, b.FirstKeyInput.Key);
        }

        [Test]
        [Description("Ensure we can parse all available Visual Studio commands")]
        public void ParseAllVsCommands()
        {
            foreach (var line in TestResources.VsCommands.Split(new string[] { Environment.NewLine },StringSplitOptions.RemoveEmptyEntries))
            {
                KeyBinding binding;
                Assert.IsTrue(KeyBinding.TryParse(line, out binding), "Could not parse - " + line);
            }
        }

        [Test]
        [Description("Ensure the re-generated strings all match the original")]
        public void CommandStringAllVsCommands()
        {
            foreach (var line in TestResources.VsCommands.Split(new string[] { Environment.NewLine },StringSplitOptions.RemoveEmptyEntries))
            {
                KeyBinding binding;
                Assert.IsTrue(KeyBinding.TryParse(line, out binding));
                Assert.AreEqual(line, binding.CommandString);
            }

        }

    }
}
