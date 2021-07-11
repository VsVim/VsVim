using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Vim.Extensions;
using Vim.Interpreter;
using Vim.UnitTest.Mock;
using Xunit;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Outlining;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public abstract class VimTest : VimTestBase
    {
        #region SimpleListener

        protected sealed class SimpleListener : IVimBufferCreationListener
        {
            internal int Count { get; set; }
            internal IVimBuffer VimBuffer { get; set; }

            void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
            {
                Count++;
                VimBuffer = vimBuffer;
            }
        }

        #endregion

        private readonly MockRepository _factory;
        private readonly Mock<IVimHost> _vimHost;
        private readonly Mock<IFileSystem> _fileSystem;
        private readonly IVimGlobalSettings _globalSettings;
        private readonly SimpleListener _simpleListener;
        private readonly IVimBufferFactory _bufferFactory;
        private readonly Dictionary<string, VariableValue> _variableMap;
        private VimImpl _vimRaw;
        private IVim _vim;

        protected VimTest(bool createVim = true)
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _globalSettings = new GlobalSettings();
            _fileSystem = _factory.Create<IFileSystem>(MockBehavior.Strict);
            _bufferFactory = VimBufferFactory;
            _simpleListener = new SimpleListener();

            _variableMap = new Dictionary<string, VariableValue>();
            _vimHost = _factory.Create<IVimHost>(MockBehavior.Strict);
            _vimHost.Setup(x => x.ShouldIncludeRcFile(It.IsAny<VimRcPath>())).Returns(true);
            _vimHost.Setup(x => x.CreateHiddenTextView()).Returns(CreateTextView());
            _vimHost.Setup(x => x.AutoSynchronizeSettings).Returns(true);
            _vimHost.Setup(x => x.VimCreated(It.IsAny<IVim>()));
            _vimHost.Setup(x => x.GetName(It.IsAny<ITextBuffer>())).Returns("VimTest.cs");
            _vimHost.Setup(x => x.EnsurePackageLoaded());
            _vimHost.SetupGet(x => x.DefaultSettings).Returns(DefaultSettings.GVim74);
            _vimHost.Setup(x => x.DoActionWhenTextViewReady(It.IsAny<FSharpFunc<Unit, Unit>>(), It.IsAny<ITextView>()))
                .Callback((FSharpFunc<Unit, Unit> action, ITextView textView) => action.Invoke(null));
            if (createVim)
            {
                CreateVim();
            }
        }

        private void CreateVim()
        {
            var creationListeners = new[] { new Lazy<IVimBufferCreationListener>(() => _simpleListener) };
            var markMap = _factory.Create<IMarkMap>();
            markMap.Setup(x => x.SetMark(Mark.LastJump, It.IsAny<IVimBufferData>(), 0, 0)).Returns(true);
            markMap.Setup(x => x.UnloadBuffer(It.IsAny<IVimBufferData>(), "VimTest.cs", 0, 0)).Returns(true);
            markMap.Setup(x => x.ReloadBuffer(It.IsAny<IVimBufferData>(), "VimTest.cs")).Returns(true);
            _vimRaw = new VimImpl(
                _vimHost.Object,
                _bufferFactory,
                CompositionContainer.GetExportedValue<IVimInterpreterFactory>(),
                creationListeners.ToFSharpList(),
                _globalSettings,
                markMap.Object,
                MockObjectFactory.CreateClipboardDevice().Object,
                _factory.Create<ISearchService>().Object,
                _fileSystem.Object,
                new VimData(_globalSettings),
                _factory.Create<IBulkOperations>().Object,
                _variableMap,
                new EditorToSettingSynchronizer(),
                new StatusUtilFactory(),
                CommonOperationsFactory,
                MouseDevice);
            _vim = _vimRaw;
            _vim.AutoLoadDigraphs = false;
            _vim.AutoLoadVimRc = false;
            _vim.AutoLoadSessionData = false;
        }

        public sealed class LoadVimRcTest : VimTest
        {
            public LoadVimRcTest()
            {
                _fileSystem
                    .Setup(x => x.GetVimRcDirectories())
                    .Returns(new string[] { })
                    .Verifiable();
                _fileSystem
                    .Setup(x => x.GetVimRcFilePaths())
                    .Returns(new VimRcPath[] { })
                    .Verifiable();
                _vimHost.Setup(x => x.VimRcLoaded(It.IsAny<VimRcState>(), It.IsAny<IVimLocalSettings>(), It.IsAny<IVimWindowSettings>()));
            }

            private void SetRcContents(params string[] lines)
            {
                var vimRcPath = new VimRcPath(VimRcKind.VimRc, "foo");
                _fileSystem
                    .Setup(x => x.GetVimRcFilePaths())
                    .Returns(new[] { vimRcPath })
                    .Verifiable();
                _fileSystem
                    .Setup(x => x.ReadAllLines(vimRcPath.FilePath))
                    .Returns(FSharpOption.Create(lines))
                    .Verifiable();
            }

            [WpfFact]
            public void BadLoadClearGlobalSettings()
            {
                _globalSettings.VimRc = "invalid";
                _globalSettings.VimRcPaths = "invalid";
                Assert.True(_vim.LoadVimRc().IsLoadFailed);
                Assert.Equal("", _globalSettings.VimRc);
                Assert.Equal("", _globalSettings.VimRcPaths);
                Assert.True(_vim.VimRcState.IsLoadFailed);
                _fileSystem.Verify();
            }

            [WpfFact]
            public void BadLoadStillChangeGlobal()
            {
                _fileSystem.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { "foo" }).Verifiable();
                Assert.True(_vim.LoadVimRc().IsLoadFailed);
                Assert.Equal("", _globalSettings.VimRc);
                Assert.Equal("foo", _globalSettings.VimRcPaths);
                Assert.True(_vim.VimRcState.IsLoadFailed);
                _fileSystem.Verify();
            }

            [WpfFact]
            public void LoadUpdateSettings()
            {
                // Setup the VimRc contents
                SetRcContents(new[] { "set ai" });
                _vimHost.Setup(x => x.CreateHiddenTextView()).Returns(CreateTextView());
                Assert.True(_vim.LoadVimRc().IsLoadSucceeded);
                Assert.True(_vimRaw._vimRcLocalSettings.AutoIndent);
                _fileSystem.Verify();
            }

            /// <summary>
            /// Part of loading the vimrc file includes creating an IVimBuffer under the hood.  This creation
            /// shouldn't show up in IVimBufferCreationListener instances.  It's designed to be transparent
            /// </summary>
            [WpfFact]
            public void LoadShouldntNotify()
            {
                SetRcContents("");
                Assert.True(_vim.LoadVimRc().IsLoadSucceeded);
                Assert.Equal(0, _simpleListener.Count);
            }

            /// <summary>
            /// The same is true for an automatic load of the VimRc file
            /// </summary>
            [WpfFact]
            public void AutoLoadShouldntNotify()
            {
                SetRcContents("");
                _vim.AutoLoadVimRc = true;
                _vim.CreateVimBuffer(CreateTextView());
                Assert.True(_vim.VimRcState.IsLoadSucceeded);
                Assert.Equal(1, _simpleListener.Count);
            }
        }

        public sealed class LoadSessionDataTest : VimTest
        {
            private readonly MemoryStream _stream;

            public LoadSessionDataTest()
            {
                _stream = new MemoryStream();
                _fileSystem
                    .Setup(x => x.Read(It.IsAny<string>()))
                    .Returns(() => FSharpOption.Create(GetStreamCopy()));
                _fileSystem
                    .Setup(x => x.Write(It.IsAny<string>(), It.IsAny<Stream>()))
                    .Callback<string, Stream>((_, stream) => stream.CopyTo(_stream))
                    .Returns(true);
                _fileSystem
                    .Setup(x => x.CreateDirectory(It.IsAny<string>()))
                    .Returns(true);
            }

            private Stream GetStreamCopy()
            {
                var stream = new MemoryStream();
                _stream.CopyTo(stream);
                stream.Position = 0;
                return stream;
            }

            private void WriteData(params string[] data)
            {
                var list = new List<SessionRegisterValue>();
                foreach (var entry in data)
                {
                    var name = entry[0];
                    var isCharacterWise = entry[1] == 'c';
                    var value = entry.Substring(2);
                    list.Add(new SessionRegisterValue(name, isCharacterWise, value, false));
                }

                var serializer = new DataContractJsonSerializer(typeof(SessionData));
                serializer.WriteObject(_stream, new SessionData(list.ToArray()));
                _stream.Position = 0;
            }

            private SessionData ReadData()
            {
                var serializer = new DataContractJsonSerializer(typeof(SessionData));
                _stream.Position = 0;
                var data = (SessionData)serializer.ReadObject(_stream);
                _stream.Position = 0;
                return data;
            }

            private void AssertRegister(char name, OperationKind kind, string value)
            {
                var register = _vim.RegisterMap.GetRegister(name);
                Assert.Equal(kind, register.OperationKind);
                Assert.Equal(value, register.StringValue);
            }

            private void WriteMacroData(params string[] data)
            {
                var list = new List<SessionRegisterValue>();
                foreach (var entry in data)
                {
                    var name = entry[0];
                    var value = entry.Substring(1);
                    list.Add(new SessionRegisterValue(name, true, value, true));
                }

                var serializer = new DataContractJsonSerializer(typeof(SessionData));
                serializer.WriteObject(_stream, new SessionData(list.ToArray()));
                _stream.Position = 0;
            }

            private void AssertMacroRegister(char name, string value)
            {
                var register = _vim.RegisterMap.GetRegister(name);
                var keyInputSet = KeyInputSetUtil.OfList(register.RegisterValue.KeyInputs);
                Assert.Equal(value, KeyNotationUtil.KeyInputSetToString(keyInputSet));
            }

            [WpfFact]
            public void PackageLoaded()
            {
                var ensuredPackageLoaded = false;
                _vimHost.Setup(x => x.EnsurePackageLoaded())
                    .Callback(() => { ensuredPackageLoaded = true; });
                _vimRaw.LoadSessionData();
                Assert.True(ensuredPackageLoaded);
            }

            [WpfFact]
            public void SimpleLoad()
            {
                WriteData("acdog", "blcat\n");
                _vimRaw.LoadSessionData();
                AssertRegister('a', OperationKind.CharacterWise, "dog");
                AssertRegister('b', OperationKind.LineWise, "cat\n");
            }

            [WpfFact]
            public void DoubleLoad()
            {
                WriteData("acdog", "blcat\n");
                _vimRaw.LoadSessionData();
                AssertRegister('a', OperationKind.CharacterWise, "dog");
                _vim.RegisterMap.SetRegisterValue('a', "cat");
                AssertRegister('a', OperationKind.CharacterWise, "cat");
                _stream.Position = 0;
                _vimRaw.LoadSessionData();
                AssertRegister('a', OperationKind.CharacterWise, "dog");
            }

            [WpfFact]
            public void SimpleSave()
            {
                _vim.RegisterMap.SetRegisterValue('h', "dog");
                _vim.RegisterMap.SetRegisterValue('i', "cat");
                _vim.SaveSessionData();
                _vim.RegisterMap.Clear();
                _stream.Position = 0;
                _vim.LoadSessionData();
                AssertRegister('h', OperationKind.CharacterWise, "dog");
                AssertRegister('i', OperationKind.CharacterWise, "cat");
            }

            [WpfFact]
            public void SaveDontSerializeAppendRegisters()
            {
                _vim.RegisterMap.SetRegisterValue('h', "dog");
                _vim.RegisterMap.SetRegisterValue('i', "cat");
                _vim.SaveSessionData();
                foreach (var sessionReg in ReadData().Registers)
                {
                    var name = NamedRegister.OfChar(sessionReg.Name).Value;
                    Assert.False(name.IsAppend);
                }
            }

            /// <summary>
            /// Macro registers can be loaded
            /// </summary>
            [WpfFact]
            public void MacroLoad()
            {
                WriteMacroData("a<Right><Left><Esc>", "babc<CR>");
                _vimRaw.LoadSessionData();
                AssertMacroRegister('a', "<Right><Left><Esc>");
                AssertMacroRegister('b', "abc<CR>");
            }

            /// <summary>
            /// Macro registers roundtrip
            /// </summary>
            [WpfFact]
            public void MacroSave()
            {
                _vim.RegisterMap.GetRegister('h').UpdateValue(
                    KeyNotationUtil.StringToKeyInputSet("<Right><Left><Esc>").KeyInputs.ToArray());
                _vim.RegisterMap.GetRegister('i').UpdateValue(
                    KeyNotationUtil.StringToKeyInputSet("abc<CR>").KeyInputs.ToArray());
                _vim.SaveSessionData();
                _vim.RegisterMap.Clear();
                _stream.Position = 0;
                _vim.LoadSessionData();
                AssertMacroRegister('h', "<Right><Left><Esc>");
                AssertMacroRegister('i', "abc<CR>");
            }

        }

        public sealed class FocussedBufferTest : VimTest
        {
            /// <summary>
            /// If the ITextView doesn't have an IVimBuffer associated with it then it should still 
            /// return None
            /// </summary>
            [WpfFact]
            public void UnknownTextView()
            {
                var textView = _factory.Create<ITextView>().Object;
                _vimHost.Setup(x => x.GetFocusedTextView()).Returns(FSharpOption.Create(textView));
                var option = _vim.FocusedBuffer;
                Assert.True(option.IsNone());
            }
        }

        public sealed class GetOrCreateVimBufferForHostTest : VimTest
        {
            /// <summary>
            /// If the host allows it then the IVimBuffer should be created 
            /// </summary>
            [WpfFact]
            public void GetOrCreateVimBufferForHost_Simple()
            {
                VimHost.ShouldCreateVimBufferImpl = true;
                var textView = CreateTextView("");

                Assert.True(Vim.TryGetOrCreateVimBufferForHost(textView, out IVimBuffer vimBuffer));
                Assert.NotNull(vimBuffer);
            }

            /// <summary>
            /// If the host doesn't allows it then the IVimBuffer shouldn't be created 
            /// </summary>
            [WpfFact]
            public void GetOrCreateVimBufferForHost_Disallow()
            {
                VimHost.ShouldCreateVimBufferImpl = false;
                var textView = CreateTextView("");

                Assert.False(Vim.TryGetOrCreateVimBufferForHost(textView, out IVimBuffer vimBuffer));
            }

            /// <summary>
            /// If it's already created then what the host says this time is irrelevant.  It's
            /// already created so the Get portion takes precedence
            /// </summary>
            [WpfFact]
            public void GetOrCreateVimBufferForHost_AlreadyCreated()
            {
                VimHost.ShouldCreateVimBufferImpl = false;
                var textView = CreateTextView("");
                Vim.CreateVimBuffer(textView);

                Assert.True(Vim.TryGetOrCreateVimBufferForHost(textView, out IVimBuffer vimBuffer));
                Assert.NotNull(vimBuffer);
            }

            /// <summary>
            /// Explicitly test the case where the IVimTextBuffer is already created and 
            /// make sure we don't run into a conflict trying to create the IVimBuffer
            /// layer on top of it 
            /// </summary>
            [WpfFact]
            public void GetOrCreateVimBufferForHost_VimTextBufferAlreadyCreated()
            {
                VimHost.ShouldCreateVimBufferImpl = true;

                var textView = CreateTextView("");
                var vimTextBuffer = Vim.GetOrCreateVimTextBuffer(textView.TextBuffer);

                Assert.True(Vim.TryGetOrCreateVimBufferForHost(textView, out IVimBuffer vimBuffer));
                Assert.Same(textView, vimBuffer.TextView);
                Assert.Same(vimTextBuffer, vimBuffer.VimTextBuffer);
            }
        }

        public sealed class MiscTest : VimTest
        {
            /// <summary>
            /// Make sure that we can close multiple IVimBuffer instances
            /// </summary>
            [WpfFact]
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

            [WpfFact]
            public void Create_SimpleTextView()
            {
                var textView = CreateTextView();
                var ret = _vim.CreateVimBuffer(textView);
                Assert.NotNull(ret);
                Assert.Same(textView, ret.TextView);
            }

            [WpfFact]
            public void Create_CreateTwiceForSameViewShouldFail()
            {
                var textView = CreateTextView();
                _vim.CreateVimBuffer(textView);
                Assert.Throws<ArgumentException>(() => _vim.CreateVimBuffer(textView));
            }

            [WpfFact]
            public void GetVimBuffer_ReturnNoneForViewThatHasNoBuffer()
            {
                var textView = CreateTextView();

                Assert.False(_vim.TryGetVimBuffer(textView, out IVimBuffer vimBuffer));
                Assert.Null(vimBuffer);
            }

            [WpfFact]
            public void GetVimBuffer_ReturnBufferForCachedCreated()
            {
                var textView = CreateTextView();
                var bufferFromCreate = _vim.CreateVimBuffer(textView);

                Assert.True(_vim.TryGetVimBuffer(textView, out IVimBuffer bufferFromGet));
                Assert.Same(bufferFromCreate, bufferFromGet);
            }

            [WpfFact]
            public void GetOrCreateVimBuffer_CreateForNewView()
            {
                var textView = CreateTextView();
                var buffer = _vim.GetOrCreateVimBuffer(textView);
                Assert.Same(textView, buffer.TextView);
            }

            [WpfFact]
            public void GetOrCreateVimBuffer_SecondCallShouldReturnAlreadyCreatedVimBuffer()
            {
                var textView = CreateTextView();
                var buffer1 = _vim.GetOrCreateVimBuffer(textView);
                var buffer2 = _vim.GetOrCreateVimBuffer(textView);
                Assert.Same(buffer1, buffer2);
            }

            [WpfFact]
            public void RemoveBuffer_ReturnFalseForNonAssociatedTextView()
            {
                var textView = CreateTextView();
                Assert.False(_vim.RemoveVimBuffer(textView));
            }

            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
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
            [WpfFact]
            public void GetOrCreateVimTextBuffer_LiveLongerThanTextView()
            {
                var textView = CreateTextView("");
                var buffer = _vim.GetOrCreateVimBuffer(textView);
                Assert.Same(buffer.VimTextBuffer, _vim.GetOrCreateVimTextBuffer(textView.TextBuffer));
                buffer.Close();
                Assert.Same(buffer.VimTextBuffer, _vim.GetOrCreateVimTextBuffer(textView.TextBuffer));
            }

            [WpfFact]
            public void RemoveBuffer_AssociatedTextView()
            {
                var textView = CreateTextView();
                _vim.CreateVimBuffer(textView);
                Assert.True(_vim.RemoveVimBuffer(textView));

                Assert.False(_vim.TryGetVimBuffer(textView, out IVimBuffer vimBuffer));
            }

            [WpfFact]
            public void ActiveBuffer1()
            {
                Assert.True(_vim.ActiveBuffer.IsNone());
            }

            [WpfFact]
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

            [WpfFact]
            public void ActiveBuffer3()
            {
                var textView = CreateTextView();
                var buffer = _vim.CreateVimBuffer(textView);
                buffer.Process('a');
                Assert.True(_vim.ActiveBuffer.IsNone());
            }
        }

        public sealed class RecentBufferTest : VimTest
        {
            private IVimBuffer CreateVimBuffer()
            {
                var textView = CreateTextView();
                var buffer = _vim.CreateVimBuffer(textView);
                return buffer;
            }

            private void FocusVimBuffer(IVimBuffer vimBuffer)
            {
                _vimRaw.OnFocus(vimBuffer);
            }

            [WpfFact]
            public void RecentBuffer()
            {
                var buffer1 = CreateVimBuffer();
                var buffer2 = CreateVimBuffer();
                var buffer3 = CreateVimBuffer();

                FocusVimBuffer(buffer1);
                FocusVimBuffer(buffer2);
                FocusVimBuffer(buffer3);
                Assert.Equal(buffer3, _vim.TryGetRecentBuffer(0).Value);
                Assert.Equal(buffer2, _vim.TryGetRecentBuffer(1).Value);
                Assert.Equal(buffer1, _vim.TryGetRecentBuffer(2).Value);
                Assert.True(_vim.TryGetRecentBuffer(3).IsNone());

                FocusVimBuffer(buffer2);
                Assert.Equal(buffer2, _vim.TryGetRecentBuffer(0).Value);
                Assert.Equal(buffer3, _vim.TryGetRecentBuffer(1).Value);
                Assert.Equal(buffer1, _vim.TryGetRecentBuffer(2).Value);
                Assert.True(_vim.TryGetRecentBuffer(3).IsNone());
            }
        }

        public sealed class GlobalSettingsCustomizationTest : VimTest
        {
            public GlobalSettingsCustomizationTest()
                : base(createVim: false)
            {
            }

            [WpfFact]
            public void Simple()
            {
                Assert.False(_globalSettings.HighlightSearch);
                _vimHost
                    .Setup(x => x.VimCreated(It.IsAny<IVim>()))
                    .Callback((IVim vim) => { vim.GlobalSettings.HighlightSearch = true; });
                CreateVim();
                Assert.True(_globalSettings.HighlightSearch);
            }
        }
    }
}
