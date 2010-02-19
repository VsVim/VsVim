using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text.Editor;
using System.IO;
using Vim.Modes.Command;

namespace VimCoreTest
{
    [TestFixture]
    public class VimTest
    {
        [SetUp]
        public void SetUp()
        {
            foreach (var value in Vim.Vim._vimRcEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(value, null);
            }
        }

        [Test]
        public void LoadVimRc1()
        {
            var settings = MockObjectFactory.CreateGlobalSettings();
            var vim = MockObjectFactory.CreateVim(settings:settings.Object);
            var service = new Mock<ITextEditorFactoryService>(MockBehavior.Strict);
            settings.SetupSet(x => x.VimRc = String.Empty).Verifiable();
            settings.SetupSet(x => x.VimRcPaths = String.Empty).Verifiable();
            Assert.IsFalse(Vim.Vim.LoadVimRc(vim.Object, service.Object));
            settings.Verify();
        }

        [Test]
        public void LoadVimRc2()
        {
            var settings = MockObjectFactory.CreateGlobalSettings();
            var vim = MockObjectFactory.CreateVim(settings: settings.Object);
            var service = new Mock<ITextEditorFactoryService>(MockBehavior.Strict);
            settings.SetupSet(x => x.VimRc = String.Empty).Verifiable();
            settings.SetupSet(x => x.VimRcPaths = @"c:\.vimrc;c:\_vimrc").Verifiable();
            Environment.SetEnvironmentVariable("HOME", @"c:\");
            Assert.IsFalse(Vim.Vim.LoadVimRc(vim.Object, service.Object));
            settings.Verify();
        }

        [Test]
        public void LoadVimRc3()
        {
            var settings = MockObjectFactory.CreateGlobalSettings();
            var vim = MockObjectFactory.CreateVim(settings: settings.Object);
            var service = new Mock<ITextEditorFactoryService>(MockBehavior.Strict);

            // Setup the buffer creation
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var commandMode = new Mock<ICommandMode>(MockBehavior.Strict);
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            commandMode.Setup(x => x.RunCommand("set noignorecase")).Verifiable();
            buffer.Setup(x => x.GetMode(ModeKind.Command)).Returns(commandMode.Object);
            service.Setup(x => x.CreateTextView()).Returns(view.Object);
            vim.Setup(x => x.CreateBuffer(view.Object)).Returns(buffer.Object);
            vim.Setup(x => x.RemoveBuffer(view.Object)).Returns(true);

            // Update the vimrc file
            var path = Environment.GetEnvironmentVariable("TMP");
            Assert.IsFalse(string.IsNullOrEmpty(path));
            var file = Path.Combine(path, ".vimrc");
            Environment.SetEnvironmentVariable("HOME", path);
            File.WriteAllText(file, "set noignorecase");

            settings.SetupProperty(x => x.VimRc);
            settings.SetupSet(x => x.VimRcPaths = It.IsAny<string>()).Verifiable();
            Assert.IsTrue(Vim.Vim.LoadVimRc(vim.Object, service.Object));
            Assert.AreEqual(file, settings.Object.VimRc);
            settings.Verify();
            commandMode.Verify();
        }
    }
}
