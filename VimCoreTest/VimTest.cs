using System;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    public sealed class VimTest : VimTestBase
    {
        private readonly MockRepository _factory;
        private readonly Mock<IMarkMap> _markMap;
        private readonly Mock<IVimHost> _vimHost;
        private readonly Mock<ISearchService> _searchInfo;
        private readonly Mock<IFileSystem> _fileSystem;
        private readonly IKeyMap _keyMap;
        private readonly IVimGlobalSettings _globalSettings;
        private readonly IVimBufferFactory _bufferFactory;
        private readonly Vim _vimRaw;
        private readonly IVim _vim;

        public VimTest()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _globalSettings = new GlobalSettings();
            _markMap = _factory.Create<IMarkMap>(MockBehavior.Strict);
            _fileSystem = _factory.Create<IFileSystem>(MockBehavior.Strict);
            _bufferFactory = VimBufferFactory;
            _keyMap = new KeyMap(_globalSettings);
            _vimHost = _factory.Create<IVimHost>(MockBehavior.Strict);
            _searchInfo = _factory.Create<ISearchService>(MockBehavior.Strict);
            _vimRaw = new Vim(
                _vimHost.Object,
                _bufferFactory,
                FSharpList<Lazy<IVimBufferCreationListener>>.Empty,
                _globalSettings,
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
        [Fact]
        public void CloseAllVimBuffers_Multiple()
        {
            const int count = 5;
            for (var i = 0; i < count; i++)
            {
                _vim.CreateVimBuffer(CreateTextView(""));
            }

            Assert.Equal(count, _vim.VimBuffers.Length);
            _vim.CloseAllVimBuffers();
            Assert.Equal(0, _vim.VimBuffers.Length);
        }

        [Fact]
        public void Create_SimpleTextView()
        {
            var textView = CreateTextView();
            var ret = _vim.CreateVimBuffer(textView);
            Assert.NotNull(ret);
            Assert.Same(textView, ret.TextView);
        }

        [Fact]
        public void Create_CreateTwiceForSameViewShouldFail()
        {
            var textView = CreateTextView();
            _vim.CreateVimBuffer(textView);
            Assert.Throws<ArgumentException>(() => _vim.CreateVimBuffer(textView));
        }

        [Fact]
        public void GetVimBuffer_ReturnNoneForViewThatHasNoBuffer()
        {
            var textView = CreateTextView();
            var ret = _vim.GetVimBuffer(textView);
            Assert.True(ret.IsNone());
        }

        [Fact]
        public void GetVimBuffer_ReturnBufferForCachedCreated()
        {
            var textView = CreateTextView();
            var bufferFromCreate = _vim.CreateVimBuffer(textView);
            var bufferFromGet = _vim.GetVimBuffer(textView);
            Assert.True(bufferFromGet.IsSome());
            Assert.Same(bufferFromGet.Value, bufferFromCreate);
        }

        [Fact]
        public void GetOrCreateVimBuffer_CreateForNewView()
        {
            var textView = CreateTextView();
            var buffer = _vim.GetOrCreateVimBuffer(textView);
            Assert.Same(textView, buffer.TextView);
        }

        [Fact]
        public void GetOrCreateVimBuffer_SecondCallShouldReturnAlreadyCreatedVimBuffer()
        {
            var textView = CreateTextView();
            var buffer1 = _vim.GetOrCreateVimBuffer(textView);
            var buffer2 = _vim.GetOrCreateVimBuffer(textView);
            Assert.Same(buffer1, buffer2);
        }

        [Fact]
        public void GetOrCreateVimBuffer_ApplyVimRcSettings()
        {
            _vim.VimRcLocalSettings.AutoIndent = true;
            _vim.VimRcLocalSettings.QuoteEscape = "b";
            var textView = CreateTextView();
            var buffer = _vim.GetOrCreateVimBuffer(textView);
            Assert.True(buffer.LocalSettings.AutoIndent);
            Assert.Equal("b", buffer.LocalSettings.QuoteEscape);
        }

        [Fact]
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
                Assert.True(buffer2.LocalSettings.AutoIndent);
                Assert.Equal("b", buffer2.LocalSettings.QuoteEscape);
                didRun = true;
            };
            buffer.Process('a');
            Assert.True(didRun);
        }

        /// <summary>
        /// Make sure window settings are applied from the current IVimBuffer to the newly 
        /// created one
        /// </summary>
        [Fact]
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
                Assert.True(buffer2.WindowSettings.CursorLine);
                didRun = true;
            };
            buffer.Process('a');
            Assert.True(didRun);
        }

        /// <summary>
        /// When creating an IVimBuffer instance, if there is an existing IVimTextBuffer then
        /// the mode should be taken from that
        /// </summary>
        [Fact]
        public void GetOrCreateVimBuffer_InitialMode()
        {
            var textView = CreateTextView("");
            _vim.GetOrCreateVimTextBuffer(textView.TextBuffer).SwitchMode(ModeKind.Insert, ModeArgument.None);
            var buffer = _vim.GetOrCreateVimBuffer(textView);
            Assert.Equal(ModeKind.Insert, buffer.ModeKind);
        }

        /// <summary>
        /// Make sure the result of this call is cached and that only one is ever created
        /// for a given ITextBuffer
        /// </summary>
        [Fact]
        public void GetOrCreateVimTextBuffer_Cache()
        {
            var textBuffer = CreateTextBuffer("");
            var vimTextBuffer = _vim.GetOrCreateVimTextBuffer(textBuffer);
            Assert.Same(vimTextBuffer, _vim.GetOrCreateVimTextBuffer(textBuffer));
        }

        /// <summary>
        /// Sanity check to ensure we create different IVimTextBuffer for different ITextBuffer 
        /// instances
        /// </summary>
        [Fact]
        public void GetOrCreateVimTextBuffer_Multiple()
        {
            var textBuffer1 = CreateTextBuffer("");
            var textBuffer2 = CreateTextBuffer("");
            var vimTextBuffer1 = _vim.GetOrCreateVimTextBuffer(textBuffer1);
            var vimTextBuffer2 = _vim.GetOrCreateVimTextBuffer(textBuffer2);
            Assert.NotSame(vimTextBuffer1, vimTextBuffer2);
        }

        /// <summary>
        /// The IVimTextBuffer should outlive an associated ITextView and IVimBuffer
        /// </summary>
        [Fact]
        public void GetOrCreateVimTextBuffer_LiveLongerThanTextView()
        {
            var textView = CreateTextView("");
            var buffer = _vim.GetOrCreateVimBuffer(textView);
            Assert.Same(buffer.VimTextBuffer, _vim.GetOrCreateVimTextBuffer(textView.TextBuffer));
            buffer.Close();
            Assert.Same(buffer.VimTextBuffer, _vim.GetOrCreateVimTextBuffer(textView.TextBuffer));
        }

        [Fact]
        public void RemoveBuffer_ReturnFalseForNonAssociatedTextView()
        {
            var textView = CreateTextView();
            Assert.False(_vim.RemoveVimBuffer(textView));
        }

        [Fact]
        public void RemoveBuffer_AssociatedTextView()
        {
            var textView = CreateTextView();
            _vim.CreateVimBuffer(textView);
            Assert.True(_vim.RemoveVimBuffer(textView));
            var ret = _vim.GetVimBuffer(textView);
            Assert.True(ret.IsNone());
        }

        [Fact]
        public void LoadVimRc1()
        {
            _globalSettings.VimRc = "invalid";
            _globalSettings.VimRcPaths = "invalid";
            _fileSystem.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { }).Verifiable();
            _fileSystem.Setup(x => x.LoadVimRcContents()).Returns(FSharpOption<FileContents>.None).Verifiable();
            Assert.False(_vim.LoadVimRc());
            _fileSystem.Verify();
            Assert.Equal("", _globalSettings.VimRc);
            Assert.Equal("", _globalSettings.VimRcPaths);
        }

        [Fact]
        public void LoadVimRc2()
        {
            _fileSystem.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { "foo" }).Verifiable();
            _fileSystem.Setup(x => x.LoadVimRcContents()).Returns(FSharpOption<FileContents>.None).Verifiable();
            Assert.False(_vim.LoadVimRc());
            Assert.Equal("", _globalSettings.VimRc);
            Assert.Equal("foo", _globalSettings.VimRcPaths);
            _fileSystem.Verify();
        }

        [Fact]
        public void LoadVimRc3()
        {
            // Setup the VimRc contents
            var contents = new FileContents(
                "foo",
                new[] { "set ai" });

            _fileSystem.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { "" }).Verifiable();
            _fileSystem.Setup(x => x.LoadVimRcContents()).Returns(FSharpOption.Create(contents)).Verifiable();
            _vimHost.Setup(x => x.CreateHiddenTextView()).Returns(CreateTextView());

            Assert.True(_vim.LoadVimRc());

            Assert.True(_vim.VimRcLocalSettings.AutoIndent);
            _fileSystem.Verify();
        }

        [Fact]
        public void ActiveBuffer1()
        {
            Assert.True(_vim.ActiveBuffer.IsNone());
        }

        [Fact]
        public void ActiveBuffer2()
        {
            var textView = CreateTextView();
            var buffer = _vim.CreateVimBuffer(textView);
            var didRun = false;
            buffer.KeyInputStart += delegate
            {
                didRun = true;
                Assert.True(_vim.ActiveBuffer.IsSome());
                Assert.Same(buffer, _vim.ActiveBuffer.Value);
            };

            buffer.Process('a');
            var active = _vim.ActiveBuffer;
            Assert.True(didRun);
        }

        [Fact]
        public void ActiveBuffer3()
        {
            var textView = CreateTextView();
            var buffer = _vim.CreateVimBuffer(textView);
            buffer.Process('a');
            Assert.True(_vim.ActiveBuffer.IsNone());
        }

    }
}
