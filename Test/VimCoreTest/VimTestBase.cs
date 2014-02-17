using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Linq;
using EditorUtils;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Vim.Interpreter;
using Vim.UI.Wpf;
using Vim.UI.Wpf.Implementation.Misc;
using Vim.UI.Wpf.Implementation.WordCompletion;
using Vim.UnitTest.Exports;
using Vim.UnitTest.Mock;
using System.Windows;

namespace Vim.UnitTest
{
    /// <summary>
    /// Standard test base for vim services which wish to do standard error monitoring like
    ///   - No dangling transactions
    ///   - No silent swallowed MEF errors
    ///   - Remove any key mappings 
    /// </summary>
    public abstract class VimTestBase : EditorHost, IDisposable
    {
        private IVim _vim;
        private IVimBufferFactory _vimBufferFactory;
        private ICommonOperationsFactory _commonOperationsFactory;
        private IVimErrorDetector _vimErrorDetector;
        private IWordUtil _wordUtil;
        private IFoldManagerFactory _foldManagerFactory;
        private IBufferTrackingService _bufferTrackingService;
        private IBulkOperations _bulkOperations;
        private IKeyUtil _keyUtil;
        private IKeyboardDevice _keyboardDevice;
        private IMouseDevice _mouseDevice;
        private IClipboardDevice _clipboardDevice;
        private IVimProtectedOperations _vimProtectedOperations;

        [ThreadStatic]
        private static CompositionContainer _compositionContainerCache;

        public override CompositionContainer CompositionContainerCache
        {
            get { return _compositionContainerCache; }
            set { _compositionContainerCache = value; }
        }

        public IVim Vim
        {
            get { return _vim; }
        }

        public VimRcState VimRcState
        {
            get { return _vim.VimRcState; }
            set { ((global::Vim.Vim)_vim).VimRcState = value; }
        }

        public IVimData VimData
        {
            get { return _vim.VimData; }
        }

        internal IVimBufferFactory VimBufferFactory
        {
            get { return _vimBufferFactory; }
        }

        public MockVimHost VimHost
        {
            get { return (MockVimHost)_vim.VimHost; }
        }

        public ICommonOperationsFactory CommonOperationsFactory
        {
            get { return _commonOperationsFactory; }
        }

        public IWordUtil WordUtil
        {
            get { return _wordUtil; }
        }

        public IFoldManagerFactory FoldManagerFactory
        {
            get { return _foldManagerFactory; }
        }

        public IBufferTrackingService BufferTrackingService
        {
            get { return _bufferTrackingService; }
        }

        public IKeyMap KeyMap
        {
            get { return _vim.KeyMap; }
        }

        public IKeyUtil KeyUtil
        {
            get { return _keyUtil; }
        }

        public IClipboardDevice ClipboardDevice
        {
            get { return _clipboardDevice; }
        }

        public IMouseDevice MouseDevice
        {
            get { return _mouseDevice; }
        }

        public IKeyboardDevice KeyboardDevice
        {
            get { return _keyboardDevice; }
        }

        public virtual bool TrackTextViewHistory
        {
            get { return true; }
        }

        public IRegisterMap RegisterMap
        {
            get { return Vim.RegisterMap; }
        }

        public Register UnnamedRegister
        {
            get { return RegisterMap.GetRegister(RegisterName.Unnamed); }
        }

        public Dictionary<string, VariableValue> VariableMap
        {
            get { return _vim.VariableMap; }
        }

        public IVimProtectedOperations VimProtectedOperations
        {
            get { return _vimProtectedOperations; }
        }

        protected VimTestBase()
        {
            // Parts of the core editor in Vs2012 depend on there being an Application.Current value else
            // they will throw a NullReferenceException.  Create one here to ensure the unit tests successfully
            // pass
            if (Application.Current == null) 
            {
                new Application();
            }

            _vim = CompositionContainer.GetExportedValue<IVim>();
            _vimBufferFactory = CompositionContainer.GetExportedValue<IVimBufferFactory>();
            _vimErrorDetector = CompositionContainer.GetExportedValue<IVimErrorDetector>();
            _commonOperationsFactory = CompositionContainer.GetExportedValue<ICommonOperationsFactory>();
            _wordUtil = CompositionContainer.GetExportedValue<IWordUtil>();
            _bufferTrackingService = CompositionContainer.GetExportedValue<IBufferTrackingService>();
            _foldManagerFactory = CompositionContainer.GetExportedValue<IFoldManagerFactory>();
            _bulkOperations = CompositionContainer.GetExportedValue<IBulkOperations>();
            _keyUtil = CompositionContainer.GetExportedValue<IKeyUtil>();
            _vimProtectedOperations = CompositionContainer.GetExportedValue<IVimProtectedOperations>();

            _keyboardDevice = CompositionContainer.GetExportedValue<IKeyboardDevice>();
            _mouseDevice = CompositionContainer.GetExportedValue<IMouseDevice>();
            _clipboardDevice = CompositionContainer.GetExportedValue<IClipboardDevice>();
            _clipboardDevice.Text = String.Empty;

            // One setting we do differ on for a default is 'timeout'.  We don't want them interfering
            // with the reliability of tests.  The default is on but turn it off here to prevent any 
            // problems
            _vim.GlobalSettings.Timeout = false;

            // Don't let the personal VimRc of the user interfere with the unit tests
            _vim.AutoLoadVimRc = false;

            // Don't show trace information in the unit tests.  It really clutters the output in an
            // xUnit run
            VimTrace.TraceSwitch.Level = TraceLevel.Off;
        }

        public virtual void Dispose()
        {
            _vim.MarkMap.Clear();

            if (_vimErrorDetector.HasErrors())
            {
                var msg = String.Format("Extension Exception: {0}", _vimErrorDetector.GetErrors().First().Message);

                // Need to clear before we throw or every subsequent test will fail with the same error
                _vimErrorDetector.Clear();

                throw new Exception(msg);
            }
            _vimErrorDetector.Clear();

            _vim.VimData.SearchHistory.Reset();
            _vim.VimData.CommandHistory.Reset();
            _vim.VimData.LastCommand = FSharpOption<StoredCommand>.None;
            _vim.VimData.LastShellCommand = FSharpOption<string>.None;
            _vim.VimData.AutoCommands = FSharpList<AutoCommand>.Empty;
            _vim.VimData.AutoCommandGroups = FSharpList<AutoCommandGroup>.Empty;

            _vim.KeyMap.ClearAll();
            _vim.KeyMap.IsZeroMappingEnabled = true;

            _vim.CloseAllVimBuffers();
            _vim.IsDisabled = false;

            // The majority of tests run without a VimRc file but a few do customize it for specific
            // test reasons.  Make sure it's consistent
            VimRcState = VimRcState.None;

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
                vimHost.ShouldCreateVimBufferImpl = false;
                vimHost.Clear();
            }

            VariableMap.Clear();
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
            IVimWindowSettings windowSettings = null,
            IWordUtil wordUtil = null)
        {
            return CreateVimBufferData(
                Vim.GetOrCreateVimTextBuffer(textView.TextBuffer),
                textView,
                statusUtil,
                jumpList,
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
            IVimWindowSettings windowSettings = null,
            IWordUtil wordUtil = null)
        {
            jumpList = jumpList ?? new JumpList(textView, _bufferTrackingService);
            statusUtil = statusUtil ?? new StatusUtil();
            windowSettings = windowSettings ?? new WindowSettings(vimTextBuffer.GlobalSettings);
            wordUtil = wordUtil ?? WordUtil;
            return new VimBufferData(
                vimTextBuffer,
                textView,
                windowSettings,
                jumpList,
                statusUtil,
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

        protected IVimBuffer CreateVimBufferWithName(string fileName, params string[] lines)
        {
            var textView = CreateTextView(lines);
            textView.TextBuffer.Properties[MockVimHost.FileNameKey] = fileName;
            return _vim.CreateVimBuffer(textView);
        }

        protected ITextStructureNavigator CreateTextStructureNavigator(ITextBuffer textBuffer, WordKind kind)
        {
            return WordUtil.CreateTextStructureNavigator(kind, textBuffer.ContentType);
        }

        internal CommandUtil CreateCommandUtil(
            IVimBufferData vimBufferData,
            IMotionUtil motionUtil = null,
            ICommonOperations operations = null,
            IFoldManager foldManager = null,
            InsertUtil insertUtil = null)
        {
            motionUtil = motionUtil ?? new MotionUtil(vimBufferData, operations);
            operations = operations ?? CommonOperationsFactory.GetCommonOperations(vimBufferData);
            foldManager = foldManager ?? VimUtil.CreateFoldManager(vimBufferData.TextView, vimBufferData.StatusUtil);
            insertUtil = insertUtil ?? new InsertUtil(vimBufferData, motionUtil, operations);
            var lineChangeTracker = new LineChangeTracker(vimBufferData);
            return new CommandUtil(
                vimBufferData,
                motionUtil,
                operations,
                foldManager,
                insertUtil,
                _bulkOperations,
                MouseDevice,
                lineChangeTracker);
        }

        protected override void GetEditorHostParts(List<ComposablePartCatalog> composablePartCatalogList, List<ExportProvider> exportProviderList)
        {
            base.GetEditorHostParts(composablePartCatalogList, exportProviderList);
            composablePartCatalogList.Add(new AssemblyCatalog(typeof(IVim).Assembly));

            // Other Exports needed to construct VsVim
            composablePartCatalogList.Add(new TypeCatalog(
                typeof(TestableClipboardDevice),
                typeof(TestableKeyboardDevice),
                typeof(TestableMouseDevice),
                typeof(global::Vim.UnitTest.Exports.VimHost),
                typeof(VimErrorDetector),
                typeof(DisplayWindowBrokerFactoryService),
                typeof(WordCompletionSessionFactoryService),
                typeof(AlternateKeyUtil),
                typeof(VimProtectedOperations),
                typeof(OutlinerTaggerProvider)));
        }

        protected void UpdateTabStop(IVimBuffer vimBuffer, int tabStop)
        {
            vimBuffer.LocalSettings.TabStop = tabStop;
            vimBuffer.LocalSettings.ExpandTab = false;
            UpdateLayout(vimBuffer.TextView);
        }

        protected void UpdateLayout(ITextView textView, int? tabStop = null)
        {
            if (tabStop.HasValue)
            {
                textView.Options.SetOptionValue(DefaultOptions.TabSizeOptionId, tabStop.Value);
            }

            // Need to force a layout here to get it to respect the tab settings
            var host = TextEditorFactoryService.CreateTextViewHost((IWpfTextView)textView, setFocus: false);
            host.HostControl.UpdateLayout();
        }
    }
}
