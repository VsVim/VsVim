using System;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class VimTest
    {
        private MockRepository _factory;
        private Mock<IMarkMap> _markMap;
        private Mock<IVimHost> _host;
        private Mock<IChangeTracker> _changeTracker;
        private Mock<ISearchService> _searchInfo;
        private IKeyMap _keyMap;
        private IVimGlobalSettings _settings;
        private IVimBufferFactory _bufferFactory;
        private Vim.Vim _vimRaw;
        private IVim _vim;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _settings = new Vim.GlobalSettings();
            _markMap = _factory.Create<IMarkMap>(MockBehavior.Strict);
            _bufferFactory = EditorUtil.FactoryService.vimBufferFactory;
            _keyMap = new KeyMap();
            _changeTracker = _factory.Create<IChangeTracker>(MockBehavior.Strict);
            _host = _factory.Create<IVimHost>(MockBehavior.Strict);
            _searchInfo = _factory.Create<ISearchService>(MockBehavior.Strict);
            _vimRaw = new Vim.Vim(
                _host.Object,
                _bufferFactory,
                FSharpList<Lazy<IVimBufferCreationListener>>.Empty,
                _settings,
                _markMap.Object,
                _keyMap,
                MockObjectFactory.CreateClipboardDevice().Object,
                _changeTracker.Object,
                _searchInfo.Object);
            _vim = _vimRaw;
        }

        [Test]
        public void Create_SimpleTextView()
        {
            var textView = EditorUtil.CreateView();
            var ret = _vim.CreateBuffer(textView);
            Assert.IsNotNull(ret);
            Assert.AreSame(textView, ret.TextView);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void Create_CreateTwiceForSameViewShouldFail()
        {
            var textView = EditorUtil.CreateView();
            _vim.CreateBuffer(textView);
            _vim.CreateBuffer(textView);
        }

        [Test]
        public void GetBuffer_ReturnNoneForViewThatHasNoBuffer()
        {
            var textView = EditorUtil.CreateView();
            var ret = _vim.GetBuffer(textView);
            Assert.IsTrue(ret.IsNone());
        }

        [Test]
        public void GetBuffer_ReturnBufferForCachedCreated()
        {
            var textView = EditorUtil.CreateView();
            var bufferFromCreate = _vim.CreateBuffer(textView);
            var bufferFromGet = _vim.GetBuffer(textView);
            Assert.IsTrue(bufferFromGet.IsSome());
            Assert.AreSame(bufferFromGet.Value, bufferFromCreate);
        }

        [Test]
        public void GetOrCreateBuffer_CreateForNewView()
        {
            var textView = EditorUtil.CreateView();
            var buffer = _vim.GetOrCreateBuffer(textView);
            Assert.AreSame(textView, buffer.TextView);
        }

        [Test]
        public void GetOrCreateBuffer_SecondCallShouldReturnAlreadyCreatedVimBuffer()
        {
            var textView = EditorUtil.CreateView();
            var buffer1 = _vim.GetOrCreateBuffer(textView);
            var buffer2 = _vim.GetOrCreateBuffer(textView);
            Assert.AreSame(buffer1, buffer2);
        }

        [Test]
        public void GetOrCreateBuffer_ApplyVimRcSettings()
        {
            _vim.VimRcLocalSettings.AutoIndent = true;
            _vim.VimRcLocalSettings.QuoteEscape = "b";
            var textView = EditorUtil.CreateView();
            var buffer = _vim.GetOrCreateBuffer(textView);
            Assert.IsTrue(buffer.Settings.AutoIndent);
            Assert.AreEqual("b", buffer.Settings.QuoteEscape);
        }

        [Test]
        public void GetOrCreateBuffer_ApplyActiveBufferSettings()
        {
            var textView = EditorUtil.CreateView();
            var buffer = _vim.GetOrCreateBuffer(textView);
            buffer.Settings.AutoIndent = true;
            buffer.Settings.QuoteEscape = "b";

            var didRun = false;
            buffer.KeyInputStart += delegate
            {
                var textView2 = EditorUtil.CreateView();
                var buffer2 = _vim.GetOrCreateBuffer(textView2);
                Assert.IsTrue(buffer2.Settings.AutoIndent);
                Assert.AreEqual("b", buffer2.Settings.QuoteEscape);
                didRun = true;
            };
            buffer.Process('a');
            Assert.IsTrue(didRun);
        }

        [Test]
        public void RemoveBuffer_ReturnFalseForNonAssociatedTextView()
        {
            var textView = EditorUtil.CreateView();
            Assert.IsFalse(_vim.RemoveBuffer(textView));
        }

        [Test]
        public void RemoveBuffer_AssociatedTextView()
        {
            var textView = EditorUtil.CreateView();
            _vim.CreateBuffer(textView);
            Assert.IsTrue(_vim.RemoveBuffer(textView));
            var ret = _vim.GetBuffer(textView);
            Assert.IsTrue(ret.IsNone());
        }

        [Test]
        public void LoadVimRc1()
        {
            _settings.VimRc = "invalid";
            _settings.VimRcPaths = "invalid";
            var fs = new Mock<IFileSystem>(MockBehavior.Strict);
            fs.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { }).Verifiable();
            fs.Setup(x => x.LoadVimRc()).Returns(FSharpOption<Tuple<string, string[]>>.None).Verifiable();
            Assert.IsFalse(_vim.LoadVimRc(fs.Object, FSharpFuncUtil.Create<Unit, ITextView>(_ => null)));
            fs.Verify();
            Assert.AreEqual("", _settings.VimRc);
            Assert.AreEqual("", _settings.VimRcPaths);
        }

        [Test]
        public void LoadVimRc2()
        {
            var fs = new Mock<IFileSystem>(MockBehavior.Strict);
            fs.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { "foo" }).Verifiable();
            fs.Setup(x => x.LoadVimRc()).Returns(FSharpOption<Tuple<string, string[]>>.None).Verifiable();
            Assert.IsFalse(_vim.LoadVimRc(fs.Object, FSharpFuncUtil.Create<Unit, ITextView>(_ => null)));
            Assert.AreEqual("", _settings.VimRc);
            Assert.AreEqual("foo", _settings.VimRcPaths);
            fs.Verify();
        }

        [Test]
        public void LoadVimRc3()
        {
            // Setup the VimRc contents
            var fileName = "foo";
            var contents = new string[] { "set ai" };
            var tuple = Tuple.Create(fileName, contents);

            var fs = new Mock<IFileSystem>(MockBehavior.Strict);
            fs.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { "" }).Verifiable();
            fs.Setup(x => x.LoadVimRc()).Returns(FSharpOption.Create(tuple)).Verifiable();

            Func<ITextView> createViewFunc = () => EditorUtil.CreateView();
            Assert.IsTrue(_vim.LoadVimRc(fs.Object, createViewFunc.ToFSharpFunc()));

            Assert.IsTrue(_vim.VimRcLocalSettings.AutoIndent);
            fs.Verify();
        }

        [Test]
        public void ActiveBuffer1()
        {
            Assert.IsTrue(_vim.ActiveBuffer.IsNone());
        }

        [Test]
        public void ActiveBuffer2()
        {
            var textView = EditorUtil.CreateView();
            var buffer = _vim.CreateBuffer(textView);
            var didRun = false;
            buffer.KeyInputStart += delegate
            {
                didRun = true;
                Assert.IsTrue(_vim.ActiveBuffer.IsSome());
                Assert.AreSame(buffer, _vim.ActiveBuffer.Value);
            };

            buffer.Process('a');
            var active = _vim.ActiveBuffer;
            Assert.IsTrue(didRun);
        }

        [Test]
        public void ActiveBuffer3()
        {
            var textView = EditorUtil.CreateView();
            var buffer = _vim.CreateBuffer(textView);
            buffer.Process('a');
            Assert.IsTrue(_vim.ActiveBuffer.IsNone());
        }

    }
}
