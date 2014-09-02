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
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UnitTest
{
    /// <summary>
    /// Standard test base for vim services which wish to do standard error monitoring like
    ///   - No dangling transactions
    ///   - No silent swallowed MEF errors
    ///   - Remove any key mappings 
    /// </summary>
    public abstract class VimTestBase : IDisposable
    {
        private readonly VimEditorHost _vimEditorHost;

        private static VimEditorHost CachedVimEditorHost;

        public CompositionContainer CompositionContainer
        {
            get { return _vimEditorHost.CompositionContainer; }
        }

        public VimEditorHost VimEditorHost
        {
            get { return _vimEditorHost; }
        }

        public ISmartIndentationService SmartIndentationService
        {
            get { return _vimEditorHost.SmartIndentationService; }
        }

        public ITextBufferFactoryService TextBufferFactoryService
        {
            get { return _vimEditorHost.TextBufferFactoryService; }
        }

        public ITextEditorFactoryService TextEditorFactoryService
        {
            get { return _vimEditorHost.TextEditorFactoryService; }
        }

        public IProjectionBufferFactoryService ProjectionBufferFactoryService
        {
            get { return _vimEditorHost.ProjectionBufferFactoryService; }
        }

        public IEditorOperationsFactoryService EditorOperationsFactoryService
        {
            get { return _vimEditorHost.EditorOperationsFactoryService; }
        }

        public IEditorOptionsFactoryService EditorOptionsFactoryService
        {
            get { return _vimEditorHost.EditorOptionsFactoryService; }
        }

        public ITextSearchService TextSearchService
        {
            get { return _vimEditorHost.TextSearchService; }
        }

        public ITextBufferUndoManagerProvider TextBufferUndoManagerProvider
        {
            get { return _vimEditorHost.TextBufferUndoManagerProvider; }
        }

        public IOutliningManagerService OutliningManagerService
        {
            get { return _vimEditorHost.OutliningManagerService; }
        }

        public IContentTypeRegistryService ContentTypeRegistryService
        {
            get { return _vimEditorHost.ContentTypeRegistryService; }
        }

        public IProtectedOperations ProtectedOperations
        {
            get { return _vimEditorHost.ProtectedOperations; }
        }

        public IBasicUndoHistoryRegistry BasicUndoHistoryRegistry
        {
            get { return _vimEditorHost.BasicUndoHistoryRegistry; }
        }

        public IVim Vim
        {
            get { return _vimEditorHost.Vim; }
        }

        public VimRcState VimRcState
        {
            get { return Vim.VimRcState; }
            set { ((global::Vim.Vim)Vim).VimRcState = value; }
        }

        public IVimData VimData
        {
            get { return _vimEditorHost.VimData; }
        }

        internal IVimBufferFactory VimBufferFactory
        {
            get { return _vimEditorHost.VimBufferFactory; }
        }

        public MockVimHost VimHost
        {
            get { return (MockVimHost)Vim.VimHost; }
        }

        public ICommonOperationsFactory CommonOperationsFactory
        {
            get { return _vimEditorHost.CommonOperationsFactory; }
        }

        public IWordUtil WordUtil
        {
            get { return _vimEditorHost.WordUtil; }
        }

        public IFoldManagerFactory FoldManagerFactory
        {
            get { return _vimEditorHost.FoldManagerFactory; }
        }

        public IBufferTrackingService BufferTrackingService
        {
            get { return _vimEditorHost.BufferTrackingService; }
        }

        public IKeyMap KeyMap
        {
            get { return _vimEditorHost.KeyMap; }
        }

        public IKeyUtil KeyUtil
        {
            get { return _vimEditorHost.KeyUtil; }
        }

        public IClipboardDevice ClipboardDevice
        {
            get { return _vimEditorHost.ClipboardDevice; }
        }

        public IMouseDevice MouseDevice
        {
            get { return _vimEditorHost.MouseDevice; }
        }

        public IKeyboardDevice KeyboardDevice
        {
            get { return _vimEditorHost.KeyboardDevice; }
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
            get { return Vim.VariableMap; }
        }

        public IVimProtectedOperations VimProtectedOperations
        {
            get { return _vimEditorHost.VimProtectedOperations; }
        }

        public IVimErrorDetector VimErrorDetector
        {
            get { return _vimEditorHost.VimErrorDetector; }
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

            _vimEditorHost = GetOrCreateVimEditorHost();
            ClipboardDevice.Text = String.Empty;

            // One setting we do differ on for a default is 'timeout'.  We don't want them interfering
            // with the reliability of tests.  The default is on but turn it off here to prevent any 
            // problems
            Vim.GlobalSettings.Timeout = false;

            // Don't let the personal VimRc of the user interfere with the unit tests
            Vim.AutoLoadVimRc = false;

            // Don't let the current directory leak into the tests
            Vim.VimData.CurrentDirectory = "";

            // Don't show trace information in the unit tests.  It really clutters the output in an
            // xUnit run
            VimTrace.TraceSwitch.Level = TraceLevel.Off;
        }

        public virtual void Dispose()
        {
            Vim.MarkMap.Clear();

            if (VimErrorDetector.HasErrors())
            {
                var msg = String.Format("Extension Exception: {0}", VimErrorDetector.GetErrors().First().Message);

                // Need to clear before we throw or every subsequent test will fail with the same error
                VimErrorDetector.Clear();

                throw new Exception(msg);
            }
            VimErrorDetector.Clear();

            Vim.VimData.SearchHistory.Reset();
            Vim.VimData.CommandHistory.Reset();
            Vim.VimData.LastCommand = FSharpOption<StoredCommand>.None;
            Vim.VimData.LastShellCommand = FSharpOption<string>.None;
            Vim.VimData.AutoCommands = FSharpList<AutoCommand>.Empty;
            Vim.VimData.AutoCommandGroups = FSharpList<AutoCommandGroup>.Empty;

            Vim.KeyMap.ClearAll();
            Vim.KeyMap.IsZeroMappingEnabled = true;

            Vim.CloseAllVimBuffers();
            Vim.IsDisabled = false;

            // The majority of tests run without a VimRc file but a few do customize it for specific
            // test reasons.  Make sure it's consistent
            VimRcState = VimRcState.None;

            // Reset all of the global settings back to their default values.   Adds quite
            // a bit of sanity to the test bed
            foreach (var setting in Vim.GlobalSettings.AllSettings)
            {
                if (!setting.IsValueDefault && !setting.IsValueCalculated)
                {
                    Vim.GlobalSettings.TrySetValue(setting.Name, setting.DefaultValue);
                }
            }

            // Reset all of the register values to empty
            foreach (var name in Vim.RegisterMap.RegisterNames)
            {
                Vim.RegisterMap.GetRegister(name).UpdateValue("");
            }

            // Don't let recording persist across tests
            if (Vim.MacroRecorder.IsRecording)
            {
                Vim.MacroRecorder.StopRecording();
            }

            var vimHost = Vim.VimHost as MockVimHost;
            if (vimHost != null)
            {
                vimHost.ShouldCreateVimBufferImpl = false;
                vimHost.Clear();
            }

            VariableMap.Clear();
        }

        public ITextBuffer CreateTextBuffer(params string[] lines)
        {
            return _vimEditorHost.CreateTextBuffer(lines);
        }

        public ITextBuffer CreateTextBuffer(IContentType contentType, params string[] lines)
        {
            return _vimEditorHost.CreateTextBuffer(contentType, lines);
        }

        public IProjectionBuffer CreateProjectionBuffer(params SnapshotSpan[] spans)
        {
            return _vimEditorHost.CreateProjectionBuffer(spans);
        }

        public IWpfTextView CreateTextView(params string[] lines)
        {
            return _vimEditorHost.CreateTextView(lines);
        }

        public IWpfTextView CreateTextView(IContentType contentType, params string[] lines)
        {
            return _vimEditorHost.CreateTextView(contentType, lines);
        }

        public IContentType GetOrCreateContentType(string type, string baseType)
        {
            return _vimEditorHost.GetOrCreateContentType(type, baseType);
        }

        /// <summary>
        /// Create an IVimTextBuffer instance with the given lines
        /// </summary>
        protected IVimTextBuffer CreateVimTextBuffer(params string[] lines)
        {
            var textBuffer = CreateTextBuffer(lines);
            return Vim.CreateVimTextBuffer(textBuffer);
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
            jumpList = jumpList ?? new JumpList(textView, BufferTrackingService);
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
            return Vim.CreateVimBuffer(textView);
        }

        /// <summary>
        /// Create an IVimBuffer instance with the given VimBufferData value
        /// </summary>
        protected IVimBuffer CreateVimBuffer(IVimBufferData vimBufferData)
        {
            return VimBufferFactory.CreateVimBuffer(vimBufferData);
        }

        protected IVimBuffer CreateVimBufferWithName(string fileName, params string[] lines)
        {
            var textView = CreateTextView(lines);
            textView.TextBuffer.Properties[MockVimHost.FileNameKey] = fileName;
            return Vim.CreateVimBuffer(textView);
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
                _vimEditorHost.BulkOperations,
                MouseDevice,
                lineChangeTracker);
        }

        private static VimEditorHost GetOrCreateVimEditorHost()
        {
            if (CachedVimEditorHost == null)
            {
                var editorHostFactory = new EditorHostFactory();
                editorHostFactory.Add(new AssemblyCatalog(typeof(IVim).Assembly));

                // Other Exports needed to construct VsVim
                editorHostFactory.Add(new TypeCatalog(
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

                var compositionContainer = editorHostFactory.CreateCompositionContainer();
                CachedVimEditorHost = new VimEditorHost(compositionContainer);
            }

            return CachedVimEditorHost;
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
