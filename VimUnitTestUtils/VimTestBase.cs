using System;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using NUnit.Framework;
using Vim.UI.Wpf;
using Vim.UI.Wpf.Implementation;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    /// <summary>
    /// Standard test base for vim services which wish to do standard error monitoring like
    ///   - No dangling transactions
    ///   - No silent swallowed MEF errors
    ///   - Remove any key mappings 
    /// </summary>
    [TestFixture]
    public abstract class VimTestBase
    {
        private CompositionContainer _compositionContainer;
        private IVim _vim;
        private IVimBufferFactory _vimBufferFactory;
        private ICommonOperationsFactory _commonOperationsFactory;
        private IVimErrorDetector _vimErrorDetector;
        private IWordUtilFactory _wordUtilFactory;
        private ITextBufferFactoryService _textBufferFactoryService;
        private ITextEditorFactoryService _textEditorFactoryService;
        private IFoldManagerFactory _foldManagerFactory;
        private IBufferTrackingService _bufferTrackingService;
        private ISmartIndentationService _smartIndentationService;
        private IProtectedOperations _protectedOperations;

        /// <summary>
        /// An IProtectedOperations value which will be properly checked in the context of this
        /// test case
        /// </summary>
        protected IProtectedOperations ProtectedOperations
        {
            get { return _protectedOperations; }
        }

        protected IVim Vim
        {
            get { return _vim; }
        }

        protected IVimBufferFactory VimBufferFactory
        {
            get { return _vimBufferFactory; }
        }

        protected MockVimHost VimHost
        {
            get { return (MockVimHost)_vim.VimHost; }
        }

        protected CompositionContainer CompositionContainer
        {
            get { return _compositionContainer; }
        }

        protected ICommonOperationsFactory CommonOperationsFactory
        {
            get { return _commonOperationsFactory; }
        }

        protected IWordUtilFactory WordUtilFactory
        {
            get { return _wordUtilFactory; }
        }

        protected IFoldManagerFactory FoldManagerFactory
        {
            get { return _foldManagerFactory; }
        }

        protected IBufferTrackingService BufferTrackingService
        {
            get { return _bufferTrackingService; }
        }

        protected ISmartIndentationService SmartIndentationService
        {
            get { return _smartIndentationService; }
        }

        [SetUp]
        public void SetupBase()
        {
            _compositionContainer = GetOrCreateCompositionContainer();
            _vim = _compositionContainer.GetExportedValue<IVim>();
            _vimBufferFactory = _compositionContainer.GetExportedValue<IVimBufferFactory>();
            _textBufferFactoryService = _compositionContainer.GetExportedValue<ITextBufferFactoryService>();
            _textEditorFactoryService = _compositionContainer.GetExportedValue<ITextEditorFactoryService>();
            _vimErrorDetector = _compositionContainer.GetExportedValue<IVimErrorDetector>();
            _commonOperationsFactory = _compositionContainer.GetExportedValue<ICommonOperationsFactory>();
            _wordUtilFactory = _compositionContainer.GetExportedValue<IWordUtilFactory>();
            _bufferTrackingService = _compositionContainer.GetExportedValue<IBufferTrackingService>();
            _foldManagerFactory = _compositionContainer.GetExportedValue<IFoldManagerFactory>();
            _smartIndentationService = _compositionContainer.GetExportedValue<ISmartIndentationService>();
            _vimErrorDetector.Clear();
            _protectedOperations = new ProtectedOperations(_vimErrorDetector);
        }

        [TearDown]
        public void TearDownBase()
        {
            if (_vimErrorDetector.HasErrors())
            {
                var msg = String.Format("Extension Exception: {0}", _vimErrorDetector.GetErrors().First().Message);
                Assert.Fail(msg);
            }

            _vim.VimData.LastCommand = FSharpOption<StoredCommand>.None;
            _vim.KeyMap.ClearAll();
            _vim.MarkMap.ClearGlobalMarks();
            _vim.CloseAllVimBuffers();

            // Reset all of the global settings back to their default values.   Adds quite
            // a bit of sanity to the test bed
            foreach (var setting in _vim.GlobalSettings.AllSettings)
            {
                if (!setting.IsValueDefault && !setting.IsValueCalculated)
                {
                    _vim.GlobalSettings.TrySetValue(setting.Name, setting.DefaultValue);
                }
            }

            // Reset all of the register values to empty
            foreach (var name in _vim.RegisterMap.RegisterNames)
            {
                _vim.RegisterMap.GetRegister(name).UpdateValue("");
            }

            // Don't let recording persist across tests
            if (_vim.MacroRecorder.IsRecording)
            {
                _vim.MacroRecorder.StopRecording();
            }

            var vimHost = Vim.VimHost as MockVimHost;
            if (vimHost != null)
            {
                vimHost.Clear();
            }
        }

        /// <summary>
        /// Create an ITextBuffer instance with the given lines
        /// </summary>
        protected ITextBuffer CreateTextBuffer(params string[] lines)
        {
            return _textBufferFactoryService.CreateTextBuffer(lines);
        }

        /// <summary>
        /// Create an IUndoRedoOperations instance with the given IStatusUtil
        /// </summary>
        protected IUndoRedoOperations CreateUndoRedoOperations(IStatusUtil statusUtil = null)
        {
            statusUtil = statusUtil ?? new StatusUtil();
            return new UndoRedoOperations(statusUtil, FSharpOption<ITextUndoHistory>.None, null);
        }

        /// <summary>
        /// Create an ITextView instance with the given lines
        /// </summary>
        protected ITextView CreateTextView(params string[] lines)
        {
            var textBuffer = CreateTextBuffer(lines);
            return _textEditorFactoryService.CreateTextView(textBuffer);
        }

        /// <summary>
        /// Create an IVimTextBuffer instance with the given lines
        /// </summary>
        protected IVimTextBuffer CreateVimTextBuffer(params string[] lines)
        {
            var textBuffer = CreateTextBuffer(lines);
            return _vim.CreateVimTextBuffer(textBuffer);
        }

        /// <summary>
        /// Create a new instance of VimBufferData.  Centralized here to make it easier to 
        /// absorb API changes in the Unit Tests
        /// </summary>
        protected VimBufferData CreateVimBufferData(
            ITextView textView,
            IStatusUtil statusUtil = null,
            IJumpList jumpList = null,
            IUndoRedoOperations undoRedoOperations = null,
            IVimWindowSettings windowSettings = null,
            IWordUtil wordUtil = null)
        {
            return CreateVimBufferData(
                Vim.GetOrCreateVimTextBuffer(textView.TextBuffer),
                textView,
                statusUtil,
                jumpList,
                undoRedoOperations,
                windowSettings,
                wordUtil);
        }

        /// <summary>
        /// Create a new instance of VimBufferData.  Centralized here to make it easier to 
        /// absorb API changes in the Unit Tests
        /// </summary>
        protected VimBufferData CreateVimBufferData(
            IVimTextBuffer vimTextBuffer,
            ITextView textView,
            IStatusUtil statusUtil = null,
            IJumpList jumpList = null,
            IUndoRedoOperations undoRedoOperations = null,
            IVimWindowSettings windowSettings = null,
            IWordUtil wordUtil = null)
        {
            jumpList = jumpList ?? new JumpList(textView, _bufferTrackingService);
            statusUtil = statusUtil ?? new StatusUtil();
            undoRedoOperations = undoRedoOperations ?? CreateUndoRedoOperations(statusUtil);
            windowSettings = windowSettings ?? new WindowSettings(vimTextBuffer.GlobalSettings);
            wordUtil = wordUtil ?? WordUtilFactory.GetWordUtil(vimTextBuffer.TextBuffer);
            return new VimBufferData(
                jumpList,
                textView,
                statusUtil,
                undoRedoOperations,
                vimTextBuffer,
                windowSettings,
                wordUtil);
        }

        /// <summary>
        /// Create a new instance of VimBufferData.  Centralized here to make it easier to 
        /// absorb API changes in the Unit Tests
        /// </summary>
        protected VimBufferData CreateVimBufferData(params string[] lines)
        {
            var textView = CreateTextView(lines);
            return CreateVimBufferData(textView);
        }

        /// <summary>
        /// Create an IVimBuffer instance with the given lines
        /// </summary>
        protected IVimBuffer CreateVimBuffer(params string[] lines)
        {
            var textView = CreateTextView(lines);
            return _vim.CreateVimBuffer(textView);
        }

        /// <summary>
        /// Create an IVimBuffer instance with the given VimBufferData value
        /// </summary>
        protected IVimBuffer CreateVimBuffer(VimBufferData vimBufferData)
        {
            return _vimBufferFactory.CreateVimBuffer(vimBufferData);
        }

        protected virtual CompositionContainer GetOrCreateCompositionContainer()
        {
            return EditorUtil.CompositionContainer;
        }
    }
}
