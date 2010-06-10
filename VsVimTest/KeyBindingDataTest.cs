using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VsVim;
using VsVim.UI;

namespace VsVimTest
{
    [TestFixture]
    public class KeyBindingDataTest
    {
        [Test]
        public void Ctor1()
        {
            var binding = KeyBinding.Parse("Global::Ctrl+Left Arrow");
            var command = new CommandKeyBinding("Foo", binding);
            var data = new KeyBindingData(new CommandKeyBinding[] { command });
            Assert.AreEqual("Ctrl+Left Arrow", data.KeyName);
            Assert.IsFalse(data.IsChecked);
        }
    }
}
