using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Linq;
using EditorUtils.UnitTest;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using NUnit.Framework;
using Vim.UI.Wpf;
using Vim.UI.Wpf.Implementation;
using Vim.UnitTest.Exports;
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
    public abstract class VimTestBase : EditorTestBase
    {
        [ThreadStatic]
        private static CompositionContainer _vimCompositionContainer;

        private IVim _vim;
        private IVimBufferFactory _vimBufferFactory;
        private ICommonOperationsFactory _commonOperationsFactory;
        private IVimErrorDetector _vimErrorDetector;
        private IWordUtilFactory _wordUtilFactory;
        private IFoldManagerFactory _foldManagerFactory;
        private IBufferTrackingService _bufferTrackingService;
        private IProtectedOperations _protectedOperations;
        private IBulkOperations _bulkOperations;

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

        protected IVimData VimData
        {
            get { return _vim.VimData; }
        }

        internal IVimBufferFactory VimBufferFactory
        {
            get { return _vimBufferFactory; }
        }

        protected MockVimHost VimHost
        {
            get { return (MockVimHost)_vim.VimHost; }
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

        protected IKeyMap KeyMap
        {
            get { return _vim.KeyMap; }
        }

        protected virtual bool TrackTextViewHistory
        {
            get { return true; }
        }

        public override void SetupBase()
        {
            base.SetupBase();
            _vim = CompositionContainer.GetExportedValue<IVim>();
            _vimBufferFactory = CompositionContainer.GetExportedValue<IVimBufferFactory>();
            _vimErrorDetector = CompositionContainer.GetExportedValue<IVimErrorDetector>();
            _commonOperationsFactory = CompositionContainer.GetExportedValue<ICommonOperationsFactory>();
            _wordUtilFactory = CompositionContainer.GetExportedValue<IWordUtilFactory>();
            _bufferTrackingService = CompositionContainer.GetExportedValue<IBufferTrackingService>();
            _foldManagerFactory = CompositionContainer.GetExportedValue<IFoldManagerFactory>();
            _bulkOperations = CompositionContainer.GetExportedValue<IBulkOperations>();
            _vimErrorDetector.Clear();
            _protectedOperations = new ProtectedOperations(_vimErrorDetector);

            // One setting we do differ on for a default is 'timeout'.  We don't want them interferring
            // with the reliability of tests.  The default is on but turn it off here to prevent any 
            // problems
            _vim.GlobalSettings.Timeout = false;

            // Don't let the personal VimRc of the user interfere with the unit tests
            _vim.AutoLoadVimRc = false;

            _vim.VimData.LastShellCommand = FSharpOption<string>.None;
            _vim.VimData.LastCommand = FSharpOption<StoredCommand>.None;
        }

        public override void  TearDownBase()
        {
            base.TearDownBase();

            if (_vimErrorDetector.HasErrors())
            {
                var msg = String.Format("Extension Exception: {0}", _vimErrorDetector.GetErrors().First().Message);
                Assert.Fail(msg);
            }
            _vimErrorDetector.Clear();

            _vim.VimData.SearchHistory.Clear();
            _vim.VimData.CommandHistory.Clear();
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
        /// Create an IUndoRedoOperations instance with the given IStatusUtil
        /// </summary>
        protected IUndoRedoOperations CreateUndoRedoOperations(IStatusUtil statusUtil = null)
        {
            statusUtil = statusUtil ?? new StatusUtil();
            return new UndoRedoOperations(statusUtil, FSharpOption<ITextUndoHistory>.None, null);
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
        protected IVimBufferData CreateVimBufferData(
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
        protected IVimBufferData CreateVimBufferData(
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
                vimTextBuffer,
                textView,
                windowSettings,
                jumpList,
                statusUtil,
                undoRedoOperations,
                wordUtil);
        }

        /// <summary>
        /// Create a new instance of VimBufferData.  Centralized here to make it easier to 
        /// absorb API changes in the Unit Tests
        /// </summary>
        protected IVimBufferData CreateVimBufferData(params string[] lines)
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
        protected IVimBuffer CreateVimBuffer(IVimBufferData vimBufferData)
        {
            return _vimBufferFactory.CreateVimBuffer(vimBufferData);
        }

        protected ITextStructureNavigator CreateTextStructureNavigator(ITextBuffer textBuffer, WordKind kind)
        {
            return WordUtilFactory.GetWordUtil(textBuffer).CreateTextStructureNavigator(kind);
        }

        internal CommandUtil CreateCommandUtil(
            IVimBufferData vimBufferData,
            IMotionUtil motionUtil = null,
            ICommonOperations operations = null,
            IFoldManager foldManager = null,
            InsertUtil insertUtil = null)
        {
            motionUtil = motionUtil ?? new MotionUtil(vimBufferData);
            operations = operations ?? CommonOperationsFactory.GetCommonOperations(vimBufferData);
            foldManager = foldManager ?? VimUtil.CreateFoldManager(vimBufferData.TextView, vimBufferData.StatusUtil);
            insertUtil = insertUtil ?? new InsertUtil(vimBufferData, operations);
            return new CommandUtil(
                vimBufferData,
                motionUtil,
                operations,
                foldManager,
                insertUtil,
                _bulkOperations);
        }

        protected override CompositionContainer GetOrCreateCompositionContainer()
        {
            if (_vimCompositionContainer == null)
            {
                var list = GetVimCatalog();
                var catalog = new AggregateCatalog(list.ToArray());
                _vimCompositionContainer = new CompositionContainer(catalog);
            }

            return _vimCompositionContainer;
        }

        protected static List<ComposablePartCatalog> GetVimCatalog()
        {
            var list = GetEditorUtilsCatalog();
            list.Add(new AssemblyCatalog(typeof(IVim).Assembly));

            // Other Exports needed to construct VsVim
            list.Add(new TypeCatalog(
                typeof(TestableClipboardDevice),
                typeof(TestableKeyboardDevice),
                typeof(MouseDevice),
                typeof(global::Vim.UnitTest.Exports.VimHost),
                typeof(VimErrorDetector),
                typeof(DisplayWindowBrokerFactoryService),
                typeof(WordCompletionSessionFactoryService)));

            return list;
        }
    }
}
