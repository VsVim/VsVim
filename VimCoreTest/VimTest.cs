using System;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class VimTest
    {
        private MockRepository _factory;
        private Mock<IVimGlobalSettings> _settings;
        private Mock<IMarkMap> _markMap;
        private Mock<IVimBufferFactory> _bufferFactory;
        private Mock<IVimHost> _host;
        private Mock<IKeyMap> _keyMap;
        private Mock<IChangeTracker> _changeTracker;
        private Mock<ISearchService> _searchInfo;
        private Vim.Vim _vimRaw;
        private IVim _vim;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _settings = _factory.Create<IVimGlobalSettings>(MockBehavior.Strict);
            _markMap = _factory.Create<IMarkMap>(MockBehavior.Strict);
            _bufferFactory = _factory.Create<IVimBufferFactory>(MockBehavior.Strict);
            _keyMap = _factory.Create<IKeyMap>(MockBehavior.Strict);
            _changeTracker = _factory.Create<IChangeTracker>(MockBehavior.Strict);
            _host = _factory.Create<IVimHost>(MockBehavior.Strict);
            _searchInfo = _factory.Create<ISearchService>(MockBehavior.Strict);
            _vimRaw = new Vim.Vim(
                _host.Object,
                _bufferFactory.Object,
                FSharpList<Lazy<IVimBufferCreationListener>>.Empty,
                _settings.Object,
                _markMap.Object,
                _keyMap.Object,
                MockObjectFactory.CreateClipboardDevice().Object,
                _changeTracker.Object,
                _searchInfo.Object);
            _vim = _vimRaw;
        }

        [Test]
        public void Create1()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _bufferFactory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
            var ret = _vim.CreateBuffer(view.Object);
            Assert.AreSame(ret, buffer.Object);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void Create2()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _bufferFactory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
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
            _bufferFactory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
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
            _bufferFactory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
            var ret = _vim.GetOrCreateBuffer(view.Object);
            Assert.AreSame(ret, buffer.Object);
        }

        [Test]
        public void GetOrCreateBuffer2()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _bufferFactory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
            var ret1 = _vim.GetOrCreateBuffer(view.Object);
            _bufferFactory.Setup(x => x.CreateBuffer(_vim, view.Object)).Throws(new Exception());
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
            _bufferFactory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);
            _vim.CreateBuffer(view.Object);
            Assert.IsTrue(_vim.RemoveBuffer(view.Object));
            var ret = _vim.GetBuffer(view.Object);
            Assert.IsTrue(ret.IsNone());
        }

        [Test]
        public void LoadVimRc1()
        {
            _settings.SetupSet(x => x.VimRc = String.Empty).Verifiable();
            _settings.SetupSet(x => x.VimRcPaths = String.Empty).Verifiable();
            var fs = new Mock<IFileSystem>(MockBehavior.Strict);
            fs.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { }).Verifiable();
            fs.Setup(x => x.LoadVimRc()).Returns(FSharpOption<Tuple<string, string[]>>.None).Verifiable();
            Assert.IsFalse(_vim.LoadVimRc(fs.Object, FSharpFuncUtil.Create<Unit, ITextView>(_ => null)));
            fs.Verify();
            _settings.Verify();
        }

        [Test]
        public void LoadVimRc2()
        {
            _settings.SetupSet(x => x.VimRc = String.Empty).Verifiable();
            _settings.SetupSet(x => x.VimRcPaths = "foo").Verifiable();
            var fs = new Mock<IFileSystem>(MockBehavior.Strict);
            fs.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { "foo" }).Verifiable();
            fs.Setup(x => x.LoadVimRc()).Returns(FSharpOption<Tuple<string, string[]>>.None).Verifiable();
            Assert.IsFalse(_vim.LoadVimRc(fs.Object, FSharpFuncUtil.Create<Unit, ITextView>(_ => null)));
            _settings.Verify();
            fs.Verify();
        }

        [Test]
        public void LoadVimRc3()
        {
            // Setup the buffer creation
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            view.Setup(x => x.Close()).Verifiable();
            var commandMode = new Mock<ICommandMode>(MockBehavior.Strict);
            var buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            commandMode.Setup(x => x.RunCommand("set noignorecase")).Returns(RunResult.Completed).Verifiable();
            buffer.Setup(x => x.CommandMode).Returns(commandMode.Object);
            var createViewFunc = FSharpFuncUtil.Create<Microsoft.FSharp.Core.Unit, ITextView>(_ => view.Object);
            _bufferFactory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer.Object);

            var fileName = "foo";
            var contents = new string[] { "set noignorecase" };
            var tuple = Tuple.Create(fileName, contents);

            _settings.SetupProperty(x => x.VimRc);
            _settings.SetupProperty(x => x.VimRcPaths);

            var fs = new Mock<IFileSystem>(MockBehavior.Strict);
            fs.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { "" }).Verifiable();
            fs.Setup(x => x.LoadVimRc()).Returns(FSharpOption.Create(tuple)).Verifiable();

            Assert.IsTrue(_vim.LoadVimRc(fs.Object, createViewFunc));
            _settings.Verify();
            commandMode.Verify();
            fs.Verify();
            view.Verify();
        }

        [Test]
        public void ActiveBuffer1()
        {
            Assert.IsTrue(_vim.ActiveBuffer.IsNone());
        }

        [Test]
        public void ActiveBuffer2()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var buffer = new MockVimBuffer();
            _bufferFactory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer);
            var ret = _vim.CreateBuffer(view.Object);
            buffer.RaiseKeyInputStart(KeyInputUtil.CharToKeyInput('c'));
            var active = _vim.ActiveBuffer;
            Assert.IsTrue(active.IsSome());
            Assert.AreSame(buffer, active.Value);
        }

        [Test]
        public void ActiveBuffer3()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            var buffer = new MockVimBuffer();
            _bufferFactory.Setup(x => x.CreateBuffer(_vim, view.Object)).Returns(buffer);
            var ret = _vim.CreateBuffer(view.Object);
            buffer.RaiseKeyInputStart(KeyInputUtil.CharToKeyInput('c'));
            buffer.RaiseKeyInputEnd(KeyInputUtil.CharToKeyInput('c'));
            Assert.IsTrue(_vim.ActiveBuffer.IsNone());
        }

    }
}
