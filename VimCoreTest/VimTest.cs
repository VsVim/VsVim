using System;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class VimTest : VimTestBase
    {
        private MockRepository _factory;
        private Mock<IMarkMap> _markMap;
        private Mock<IVimHost> _vimHost;
        private Mock<ISearchService> _searchInfo;
        private Mock<IFileSystem> _fileSystem;
        private IKeyMap _keyMap;
        private IVimGlobalSettings _settings;
        private IVimBufferFactory _bufferFactory;
        private Vim _vimRaw;
        private IVim _vim;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _settings = new GlobalSettings();
            _markMap = _factory.Create<IMarkMap>(MockBehavior.Strict);
            _fileSystem = _factory.Create<IFileSystem>(MockBehavior.Strict);
            _bufferFactory = VimBufferFactory;
            _keyMap = new KeyMap();
            _vimHost = _factory.Create<IVimHost>(MockBehavior.Strict);
            _searchInfo = _factory.Create<ISearchService>(MockBehavior.Strict);
            _vimRaw = new Vim(
                _vimHost.Object,
                _bufferFactory,
                FSharpList<Lazy<IVimBufferCreationListener>>.Empty,
                _settings,
                _markMap.Object,
                _keyMap,
                MockObjectFactory.CreateClipboardDevice().Object,
                _searchInfo.Object,
                _fileSystem.Object,
                new VimData(),
                _factory.Create<IBulkOperations>().Object);
            _vim = _vimRaw;
            _vim.AutoLoadVimRc = false;
        }

        /// <summary>
        /// Make sure that we can close multiple IVimBuffer instances
        /// </summary>
        [Test]
        public void CloseAllVimBuffers_Multiple()
        {
            const int count = 5;
            for (var i = 0; i < count; i++)
            {
                _vim.CreateVimBuffer(CreateTextView(""));
            }

            Assert.AreEqual(count, _vim.VimBuffers.Length);
            _vim.CloseAllVimBuffers();
            Assert.AreEqual(0, _vim.VimBuffers.Length);
        }

        [Test]
        public void Create_SimpleTextView()
        {
            var textView = CreateTextView();
            var ret = _vim.CreateVimBuffer(textView);
            Assert.IsNotNull(ret);
            Assert.AreSame(textView, ret.TextView);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void Create_CreateTwiceForSameViewShouldFail()
        {
            var textView = CreateTextView();
            _vim.CreateVimBuffer(textView);
            _vim.CreateVimBuffer(textView);
        }

        [Test]
        public void GetVimBuffer_ReturnNoneForViewThatHasNoBuffer()
        {
            var textView = CreateTextView();
            var ret = _vim.GetVimBuffer(textView);
            Assert.IsTrue(ret.IsNone());
        }

        [Test]
        public void GetVimBuffer_ReturnBufferForCachedCreated()
        {
            var textView = CreateTextView();
            var bufferFromCreate = _vim.CreateVimBuffer(textView);
            var bufferFromGet = _vim.GetVimBuffer(textView);
            Assert.IsTrue(bufferFromGet.IsSome());
            Assert.AreSame(bufferFromGet.Value, bufferFromCreate);
        }

        [Test]
        public void GetOrCreateVimBuffer_CreateForNewView()
        {
            var textView = CreateTextView();
            var buffer = _vim.GetOrCreateVimBuffer(textView);
            Assert.AreSame(textView, buffer.TextView);
        }

        [Test]
        public void GetOrCreateVimBuffer_SecondCallShouldReturnAlreadyCreatedVimBuffer()
        {
            var textView = CreateTextView();
            var buffer1 = _vim.GetOrCreateVimBuffer(textView);
            var buffer2 = _vim.GetOrCreateVimBuffer(textView);
            Assert.AreSame(buffer1, buffer2);
        }

        [Test]
        public void GetOrCreateVimBuffer_ApplyVimRcSettings()
        {
            _vim.VimRcLocalSettings.AutoIndent = true;
            _vim.VimRcLocalSettings.QuoteEscape = "b";
            var textView = CreateTextView();
            var buffer = _vim.GetOrCreateVimBuffer(textView);
            Assert.IsTrue(buffer.LocalSettings.AutoIndent);
            Assert.AreEqual("b", buffer.LocalSettings.QuoteEscape);
        }

        [Test]
        public void GetOrCreateVimBuffer_ApplyActiveBufferLocalSettings()
        {
            var textView = CreateTextView();
            var buffer = _vim.GetOrCreateVimBuffer(textView);
            buffer.LocalSettings.AutoIndent = true;
            buffer.LocalSettings.QuoteEscape = "b";

            var didRun = false;
            buffer.KeyInputStart += delegate
            {
                var textView2 = CreateTextView();
                var buffer2 = _vim.GetOrCreateVimBuffer(textView2);
                Assert.IsTrue(buffer2.LocalSettings.AutoIndent);
                Assert.AreEqual("b", buffer2.LocalSettings.QuoteEscape);
                didRun = true;
            };
            buffer.Process('a');
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Make sure window settings are applied from the current IVimBuffer to the newly 
        /// created one
        /// </summary>
        [Test]
        public void GetOrCreateVimBuffer_ApplyActiveBufferWindowSettings()
        {
            var textView = CreateTextView();
            var buffer = _vim.GetOrCreateVimBuffer(textView);
            buffer.WindowSettings.CursorLine = true;

            var didRun = false;
            buffer.KeyInputStart += delegate
            {
                var textView2 = CreateTextView();
                var buffer2 = _vim.GetOrCreateVimBuffer(textView2);
                Assert.IsTrue(buffer2.WindowSettings.CursorLine);
                didRun = true;
            };
            buffer.Process('a');
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// When creating an IVimBuffer instance, if there is an existing IVimTextBuffer then
        /// the mode should be taken from that
        /// </summary>
        [Test]
        public void GetOrCreateVimBuffer_InitialMode()
        {
            var textView = CreateTextView("");
            _vim.GetOrCreateVimTextBuffer(textView.TextBuffer).SwitchMode(ModeKind.Insert, ModeArgument.None);
            var buffer = _vim.GetOrCreateVimBuffer(textView);
            Assert.AreEqual(ModeKind.Insert, buffer.ModeKind);
        }

        /// <summary>
        /// Make sure the result of this call is cached and that only one is ever created
        /// for a given ITextBuffer
        /// </summary>
        [Test]
        public void GetOrCreateVimTextBuffer_Cache()
        {
            var textBuffer = CreateTextBuffer("");
            var vimTextBuffer = _vim.GetOrCreateVimTextBuffer(textBuffer);
            Assert.AreSame(vimTextBuffer, _vim.GetOrCreateVimTextBuffer(textBuffer));
        }

        /// <summary>
        /// Sanity check to ensure we create different IVimTextBuffer for different ITextBuffer 
        /// instances
        /// </summary>
        [Test]
        public void GetOrCreateVimTextBuffer_Multiple()
        {
            var textBuffer1 = CreateTextBuffer("");
            var textBuffer2 = CreateTextBuffer("");
            var vimTextBuffer1 = _vim.GetOrCreateVimTextBuffer(textBuffer1);
            var vimTextBuffer2 = _vim.GetOrCreateVimTextBuffer(textBuffer2);
            Assert.AreNotSame(vimTextBuffer1, vimTextBuffer2);
        }

        /// <summary>
        /// The IVimTextBuffer should outlive an associated ITextView and IVimBuffer
        /// </summary>
        [Test]
        public void GetOrCreateVimTextBuffer_LiveLongerThanTextView()
        {
            var textView = CreateTextView("");
            var buffer = _vim.GetOrCreateVimBuffer(textView);
            Assert.AreSame(buffer.VimTextBuffer, _vim.GetOrCreateVimTextBuffer(textView.TextBuffer));
            buffer.Close();
            Assert.AreSame(buffer.VimTextBuffer, _vim.GetOrCreateVimTextBuffer(textView.TextBuffer));
        }

        [Test]
        public void RemoveBuffer_ReturnFalseForNonAssociatedTextView()
        {
            var textView = CreateTextView();
            Assert.IsFalse(_vim.RemoveVimBuffer(textView));
        }

        [Test]
        public void RemoveBuffer_AssociatedTextView()
        {
            var textView = CreateTextView();
            _vim.CreateVimBuffer(textView);
            Assert.IsTrue(_vim.RemoveVimBuffer(textView));
            var ret = _vim.GetVimBuffer(textView);
            Assert.IsTrue(ret.IsNone());
        }

        [Test]
        public void LoadVimRc1()
        {
            _settings.VimRc = "invalid";
            _settings.VimRcPaths = "invalid";
            _fileSystem.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { }).Verifiable();
            _fileSystem.Setup(x => x.LoadVimRcContents()).Returns(FSharpOption<FileContents>.None).Verifiable();
            Assert.IsFalse(_vim.LoadVimRc());
            _fileSystem.Verify();
            Assert.AreEqual("", _settings.VimRc);
            Assert.AreEqual("", _settings.VimRcPaths);
        }

        [Test]
        public void LoadVimRc2()
        {
            _fileSystem.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { "foo" }).Verifiable();
            _fileSystem.Setup(x => x.LoadVimRcContents()).Returns(FSharpOption<FileContents>.None).Verifiable();
            Assert.IsFalse(_vim.LoadVimRc());
            Assert.AreEqual("", _settings.VimRc);
            Assert.AreEqual("foo", _settings.VimRcPaths);
            _fileSystem.Verify();
        }

        [Test]
        public void LoadVimRc3()
        {
            // Setup the VimRc contents
            var contents = new FileContents(
                "foo",
                new[] { "set ai" });

            _fileSystem.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { "" }).Verifiable();
            _fileSystem.Setup(x => x.LoadVimRcContents()).Returns(FSharpOption.Create(contents)).Verifiable();
            _vimHost.Setup(x => x.CreateHiddenTextView()).Returns(CreateTextView());

            Assert.IsTrue(_vim.LoadVimRc());

            Assert.IsTrue(_vim.VimRcLocalSettings.AutoIndent);
            _fileSystem.Verify();
        }

        [Test]
        public void ActiveBuffer1()
        {
            Assert.IsTrue(_vim.ActiveBuffer.IsNone());
        }

        [Test]
        public void ActiveBuffer2()
        {
            var textView = CreateTextView();
            var buffer = _vim.CreateVimBuffer(textView);
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
            var textView = CreateTextView();
            var buffer = _vim.CreateVimBuffer(textView);
            buffer.Process('a');
            Assert.IsTrue(_vim.ActiveBuffer.IsNone());
        }

    }
}
