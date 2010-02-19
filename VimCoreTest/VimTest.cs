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
        private Mock<IVimGlobalSettings> _settings;
        private Mock<IRegisterMap> _registerMap;
        private Mock<IMarkMap> _markMap;
        private Mock<IVimBufferFactory> _factory;
        private Mock<IVimHost> _host;
        private Mock<ITextEditorFactoryService> _editorFactoryService;
        private Vim.Vim _vimRaw;
        private IVim _vim;

        [SetUp]
        public void Setup()
        {
            _settings = new Mock<IVimGlobalSettings>(MockBehavior.Strict);
            _registerMap = new Mock<IRegisterMap>(MockBehavior.Strict);
            _markMap = new Mock<IMarkMap>(MockBehavior.Strict);
            _factory = new Mock<IVimBufferFactory>(MockBehavior.Strict);
            _editorFactoryService = new Mock<ITextEditorFactoryService>(MockBehavior.Strict);
            _host = new Mock<IVimHost>(MockBehavior.Strict);
            _vimRaw = new Vim.Vim(
                _host.Object,
                _factory.Object,
                _editorFactoryService.Object,
                _settings.Object,
                _registerMap.Object,
                _markMap.Object);
            _vim = _vimRaw;
        }

        [Test]
        public void Create1()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _factory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
            var ret = _vim.CreateBuffer(view.Object);
            Assert.AreSame(ret, buffer.Object);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void Create2()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _factory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
            var ret = _vim.CreateBuffer(view.Object);
            var ret2 = _vim.CreateBuffer(view.Object);
        }

        [Test]
        public void GetBuffer1()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var ret = _vim.GetBuffer(view.Object);
            Assert.IsTrue(ret.IsNone());
        }

        [Test]
        public void GetBuffer2()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _factory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
            _vim.CreateBuffer(view.Object);
            var ret = _vim.GetBuffer(view.Object);
            Assert.IsTrue(ret.IsSome());
            Assert.AreSame(ret.Value, buffer.Object);
        }

        [Test]
        public void GetOrCreateBuffer1()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _factory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
            var ret = _vim.GetOrCreateBuffer(view.Object);
            Assert.AreSame(ret, buffer.Object);
        }

        [Test]
        public void GetOrCreateBuffer2()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _factory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
            var ret1 = _vim.GetOrCreateBuffer(view.Object);
            _factory.Setup(x => x.CreateBuffer(_vim, view.Object)).Throws(new Exception());
            var ret2 = _vim.GetOrCreateBuffer(view.Object);
            Assert.AreSame(ret1, ret2);
        }

        [Test]
        public void RemoveBuffer1()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            Assert.IsFalse(_vim.RemoveBuffer(view.Object));
        }

        [Test]
        public void RemoveBuffer2()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _factory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
            _vim.CreateBuffer(view.Object);
            Assert.IsTrue(_vim.RemoveBuffer(view.Object));
            var ret = _vim.GetBuffer(view.Object);
            Assert.IsTrue(ret.IsNone());
        }
    }

    [TestFixture]
    public class VimOldTest
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
