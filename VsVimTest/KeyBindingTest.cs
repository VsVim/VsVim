using System;
using System.Linq;
using NUnit.Framework;
using Vim;
using KeyBinding = VsVim.KeyBinding;

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
            Assert.AreEqual(VimKey.F2, b.FirstKeyInput.Key);
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

        [Test]
        public void VsKeyBackSpace()
        {
            var b = KeyBinding.Parse("::Bkspce");
            Assert.AreEqual(VimKey.Back, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyLeftArrow()
        {
            var b = KeyBinding.Parse("::Left Arrow");
            Assert.AreEqual(VimKey.Left, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyRightArrow()
        {
            var b = KeyBinding.Parse("::Right Arrow");
            Assert.AreEqual(VimKey.Right, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyUpArrow()
        {
            var b = KeyBinding.Parse("::Up Arrow");
            Assert.AreEqual(VimKey.Up, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyDownArrow()
        {
            var b = KeyBinding.Parse("::Down Arrow");
            Assert.AreEqual(VimKey.Down, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyDownArrow2()
        {
            var b = KeyBinding.Parse("::Shift+Down Arrow");
            Assert.AreEqual(VimKey.Down, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyPageDown()
        {
            var b = KeyBinding.Parse("::PgDn");
            Assert.AreEqual(VimKey.PageDown, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsKeyPageUp()
        {
            var b = KeyBinding.Parse("::PgUp");
            Assert.AreEqual(VimKey.PageUp, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsNum1()
        {
            var b = KeyBinding.Parse("::Num +");
            Assert.AreEqual(VimKey.KeypadPlus, b.FirstKeyInput.Key);
        }

        [Test]
        public void VsNum2()
        {
            var b = KeyBinding.Parse("::Num *");
            Assert.AreEqual(VimKey.KeypadMultiply, b.FirstKeyInput.Key);
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
