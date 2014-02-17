using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using EditorUtils;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;
using Vim.UI.Wpf.UnitTest;
using Vim.UnitTest;
using VsVim.Implementation.Misc;
using VsVim.Implementation.ReSharper;

namespace VsVim.UnitTest.Utils
{
    /// <summary>
    /// This class attempts to simulate key input in Visual Studio.  The intent
    /// is to create an environment which helps out our testing efforts in understanding
    /// the Visual Studio input process.
    ///
    /// This class is specifically designed for testing purposes so the heavy use of
    /// mocking is acceptable
    ///
    /// This class is far from perfect and I expect it to evolve over time. As it evolves though
    /// the test cases which use it will pick up all of the new information we gather though 
    /// and this will add even more value to our unit tests
    /// </summary>
    internal sealed class VsSimulation
    {
        #region VsKeyboardInputSimulation

        /// <summary>
        /// This is a Visual Studio specific implementation of the key processor.  It takes into account the interaction
        /// between IOleCommandTarget and keyboard input. 
        /// </summary>
        private sealed class VsKeyboardInputSimulation : KeyboardInputSimulation
        {
            private readonly VsSimulation _vsSimulation;

            internal VsKeyboardInputSimulation(VsSimulation vsSimulation, IWpfTextView wpfTextView) : base(wpfTextView)
            {
                _vsSimulation = vsSimulation;
            }

            /// <summary>
            /// Visual Studio hooks PreTranslateMessage and will process keyboard input there if it maps to a 
            /// command keyboard binding.  Textual input is *not* handled here but keys like Esc, Up, Down, etc ...
            /// are.  They need to be routed directly to IOleCommandTarget
            /// </summary>
            protected override bool PreProcess(KeyDirection keyDirection, KeyInput keyInput, Key key, ModifierKeys modifierKeys)
            {
                // Visual Studio only intercepts the down events.  
                if (keyDirection != KeyDirection.Down)
                {
                    return false;
                }

                switch (keyInput.Key)
                {
                    case VimKey.Escape:
                    case VimKey.Back:
                    case VimKey.Up:
                    case VimKey.Down:
                    case VimKey.Left:
                    case VimKey.Right:
                    case VimKey.Tab:
                        return _vsSimulation.RunInOleCommandTarget(keyInput);
                    default:
                        return false;
                }
            }
        }

        #endregion

        #region SimulationKeyProcessor

        /// <summary>
        /// Visual Studio inserts a KeyProcess into the chain which turns TextInput events
        /// into TypeChar commands.  Simulate that here
        /// </summary>
        private sealed class SimulationKeyProcessor : KeyProcessor
        {
            private readonly ITextView _textView;

            internal SimulationKeyProcessor(ITextView textView)
            {
                _textView = textView;
            }

            /// <summary>
            /// Grab the IOleCommandTarget associated with the ITextView.  Need to grab it in the 
            /// same manner as Visual Studio to properly simulate input
            /// </summary>
            private IOleCommandTarget OleCommandTarget
            {
                get { return _textView.Properties.GetProperty<IOleCommandTarget>(typeof(IOleCommandTarget)); }
            }

            public override void TextInput(TextCompositionEventArgs args)
            {
                var text = args.Text;
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                foreach (var cur in text)
                {
                    SendTypeChar(cur);
                }

                args.Handled = true;
            }

            private void SendTypeChar(char c)
            {
                using (var oleCommandData = OleCommandData.CreateTypeChar(c))
                {
                    OleCommandTarget.Exec(oleCommandData);
                }
            }
        }

        #endregion

        #region DefaultCommandTarget

        /// <summary>
        /// Represents the end of the IOleCommandTarget chain.  This is where Visual Studio will 
        /// actually modify the ITextBuffer in response to commands.
        /// 
        /// This mimics mainly the implementation VsTextViewAdapter InnerExec and InnerQueryStatus
        /// </summary>
        internal sealed class DefaultCommandTarget : IOleCommandTarget
        {
            private readonly ITextView _textView;
            private readonly IEditorOperations _editorOperatins;
            private EditCommand _lastExecEditCommand;
            private EditCommand _lastQueryStatusEditCommand;

            internal EditCommand LastExecEditCommand
            {
                get { return _lastExecEditCommand; }
            }

            internal EditCommand LastQueryStatusEditCommand
            {
                get { return _lastQueryStatusEditCommand; }
            }

            internal DefaultCommandTarget(ITextView textView, IEditorOperations editorOperations)
            {
                _textView = textView;
                _editorOperatins = editorOperations;
            }

            /// <summary>
            /// Try and exec the given KeyInput
            /// </summary>
            private bool TryExec(KeyInput keyInput)
            {
                switch (keyInput.Key)
                {
                    case VimKey.Left:
                        _editorOperatins.MoveToPreviousCharacter(extendSelection: keyInput.KeyModifiers == KeyModifiers.Shift);
                        return true;
                    case VimKey.Right:
                        _editorOperatins.MoveToNextCharacter(extendSelection: keyInput.KeyModifiers == KeyModifiers.Shift);
                        return true;
                    case VimKey.Up:
                        _editorOperatins.MoveLineUp(extendSelection: keyInput.KeyModifiers == KeyModifiers.Shift);
                        return true;
                    case VimKey.Down:
                        _editorOperatins.MoveLineDown(extendSelection: keyInput.KeyModifiers == KeyModifiers.Shift);
                        return true;
                    case VimKey.Back:
                        _editorOperatins.Backspace();
                        return true;
                    case VimKey.Tab:
                        if (keyInput.KeyModifiers == KeyModifiers.Shift)
                        {
                            _editorOperatins.Unindent();
                        }
                        else
                        {
                            _editorOperatins.Indent();
                        }
                        return true;
                }

                if (Char.IsLetterOrDigit(keyInput.Char))
                {
                    _editorOperatins.InsertText(keyInput.Char.ToString());
                    return true;
                }

                return false;
            }

            int IOleCommandTarget.Exec(ref Guid commandGroup, uint cmdId, uint cmdExecOpt, IntPtr variantIn, IntPtr variantOut)
            {
                EditCommand editCommand;
                if (!OleCommandUtil.TryConvert(commandGroup, cmdId, variantIn, KeyModifiers.None, out editCommand))
                {
                    _lastExecEditCommand = null;
                    return VSConstants.E_FAIL;
                }

                _lastExecEditCommand = editCommand;
                return TryExec(editCommand.KeyInput) ? VSConstants.S_OK : VSConstants.E_FAIL;
            }

            int IOleCommandTarget.QueryStatus(ref Guid commandGroup, uint commandCount, OLECMD[] commands, IntPtr commandText)
            {
                EditCommand editCommand;
                if (1 != commandCount || !OleCommandUtil.TryConvert(commandGroup, commands[0].cmdID, commandText, KeyModifiers.None, out editCommand))
                {
                    _lastQueryStatusEditCommand = null;
                    commands[0].cmdf = 0;
                    return NativeMethods.S_OK;
                }

                _lastQueryStatusEditCommand = editCommand;
                commands[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                return NativeMethods.S_OK;
            }
        }

        #endregion

        /// <summary>
        /// Cache of QueryStatus commands
        /// </summary>
        private readonly Dictionary<CommandId, bool> _cachedQueryStatusMap = new Dictionary<CommandId, bool>();

        /// <summary>
        /// Head of the IOleCommandTarget chain
        /// </summary>
        private readonly IOleCommandTarget _commandTarget;

        /// <summary>
        /// This is the default command target for Visual Studio.  It simulates the final command
        /// target on the chain.
        /// </summary>
        private readonly DefaultCommandTarget _vsCommandTarget;

        private readonly IWpfTextView _wpfTextView;
        private readonly VsKeyboardInputSimulation _vsKeyboardInputSimulation;
        private readonly MockRepository _factory;
        private readonly Mock<IVsAdapter> _vsAdapter;
        private readonly Mock<IDisplayWindowBroker> _displayWindowBroker;
        private readonly Mock<IReportDesignerUtil> _reportDesignerUtil;
        private readonly TestableSynchronizationContext _testableSynchronizationContext;
        private readonly IKeyUtil _keyUtil;
        private readonly ReSharperCommandTargetSimulation _reSharperCommandTarget;
        private bool _simulateStandardKeyMappings;

        internal bool SimulateStandardKeyMappings
        {
            get { return _simulateStandardKeyMappings; }
            set { _simulateStandardKeyMappings = value; }
        }

        /// <summary>
        /// In the case where we are simulating R# this will be the command target used 
        /// </summary>
        internal ReSharperCommandTargetSimulation ReSharperCommandTargetOpt
        {
            get { return _reSharperCommandTarget; }
        }

        internal Mock<IDisplayWindowBroker> DisplayWindowBroker
        {
            get { return _displayWindowBroker; }
        }

        internal DefaultCommandTarget VsCommandTarget
        {
            get { return _vsCommandTarget; }
        }

        internal VsSimulation(IVimBufferCoordinator bufferCoordinator, bool simulateResharper, bool simulateStandardKeyMappings, IEditorOperationsFactoryService editorOperationsFactoryService, IKeyUtil keyUtil)
        {
            _keyUtil = keyUtil;
            _wpfTextView = (IWpfTextView)bufferCoordinator.VimBuffer.TextView;
            _factory = new MockRepository(MockBehavior.Strict);
            _vsKeyboardInputSimulation = new VsKeyboardInputSimulation(this, _wpfTextView);
            _testableSynchronizationContext = new TestableSynchronizationContext();
            _simulateStandardKeyMappings = simulateStandardKeyMappings;

            // Create the IVsAdapter and pick reasonable defaults here.  Consumers can modify 
            // this via an exposed property
            _vsAdapter = _factory.Create<IVsAdapter>();
            _vsAdapter.SetupGet(x => x.InAutomationFunction).Returns(false);
            _vsAdapter.SetupGet(x => x.KeyboardDevice).Returns(_vsKeyboardInputSimulation.KeyBoardDevice);
            _vsAdapter.Setup(x => x.IsReadOnly(It.IsAny<ITextBuffer>())).Returns(false);
            _vsAdapter.Setup(x => x.IsReadOnly(It.IsAny<ITextView>())).Returns(false);
            _vsAdapter.Setup(x => x.IsIncrementalSearchActive(_wpfTextView)).Returns(false);

            _reportDesignerUtil = _factory.Create<IReportDesignerUtil>();
            _reportDesignerUtil.Setup(x => x.IsExpressionView(_wpfTextView)).Returns(false);

            _displayWindowBroker = _factory.Create<IDisplayWindowBroker>();
            _displayWindowBroker.SetupGet(x => x.IsCompletionActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsQuickInfoActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsSignatureHelpActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsSmartTagSessionActive).Returns(false);

            _vsCommandTarget = new DefaultCommandTarget(
                bufferCoordinator.VimBuffer.TextView,
                editorOperationsFactoryService.GetEditorOperations(bufferCoordinator.VimBuffer.TextView));

            var textManager = _factory.Create<ITextManager>();
            var commandTargets = new List<ICommandTarget>();
            if (simulateResharper)
            {
                commandTargets.Add(ReSharperKeyUtil.GetOrCreate(bufferCoordinator));
            }
            commandTargets.Add(new StandardCommandTarget(bufferCoordinator, textManager.Object, _displayWindowBroker.Object, _vsCommandTarget));

            // Create the VsCommandTarget.  It's next is the final and default Visual Studio 
            // command target
            var vsCommandTarget = new VsCommandTarget(
                bufferCoordinator,
                textManager.Object,
                _vsAdapter.Object,
                _displayWindowBroker.Object,
                _keyUtil,
                _vsCommandTarget,
                commandTargets.ToReadOnlyCollectionShallow());

            // Time to setup the start command target.  If we are simulating R# then put them ahead of VsVim
            // on the IOleCommandTarget chain.  VsVim doesn't try to fight R# and prefers instead to be 
            // behind them
            if (simulateResharper)
            {
                _reSharperCommandTarget = new ReSharperCommandTargetSimulation(_wpfTextView, vsCommandTarget);
                _commandTarget = _reSharperCommandTarget;
            }
            else
            {
                _commandTarget = vsCommandTarget;
            }

            // Visual Studio hides the default IOleCommandTarget inside of the IWpfTextView property
            // bag.  The default KeyProcessor implementation looks here for IOleCommandTarget to 
            // process text input.  
            //
            // This should always point to the head of the IOleCommandTarget chain.  In the implementation
            // it actually points to the IVsTextView implementation which then immediately routes to the
            // IOleCommandTarget head
            _wpfTextView.Properties[typeof(IOleCommandTarget)] = _commandTarget;

            // Create the input controller.  Make sure that the VsVim one is ahead in the list
            // from the default Visual Studio one.  We can guarantee this is true due to MEF 
            // ordering of the components
            if (simulateResharper)
            {
                _vsKeyboardInputSimulation.KeyProcessors.Add(ReSharperKeyUtil.GetOrCreate(bufferCoordinator));
            }
            _vsKeyboardInputSimulation.KeyProcessors.Add(new VsKeyProcessor(_vsAdapter.Object, bufferCoordinator, _keyUtil, _reportDesignerUtil.Object));
            _vsKeyboardInputSimulation.KeyProcessors.Add((KeyProcessor)bufferCoordinator);
            _vsKeyboardInputSimulation.KeyProcessors.Add(new SimulationKeyProcessor(bufferCoordinator.VimBuffer.TextView));
        }

        /// <summary>
        /// Run the specified VimKey against the buffer
        /// </summary>
        internal void Run(params VimKey[] keys)
        {
            foreach (var key in keys)
            {
                Run(KeyInputUtil.VimKeyToKeyInput(key));
            }
        }

        /// <summary>
        /// Run the specified set of characters against the buffer
        /// </summary>
        internal void Run(string text)
        {
            foreach (var cur in text)
            {
                this.Run(cur);
            }
        }

        /// <summary>
        /// Run the specified character against the buffer
        /// </summary>
        internal void Run(char c)
        {
            Run(KeyInputUtil.CharToKeyInput(c));
        }

        /// <summary>
        /// Run the given KeyInput against the Visual Studio simulation
        /// </summary>
        internal void Run(KeyInput keyInput)
        {
            _testableSynchronizationContext.Install();
            try
            {
                _vsKeyboardInputSimulation.Run(keyInput);
                _testableSynchronizationContext.RunAll();
            }
            finally
            {
                _testableSynchronizationContext.Uninstall();
            }
        }

        /// <summary>
        /// Run the KeyInput through IOleCommandTarget.  This is the primary entry point for 
        /// </summary>
        private bool RunInOleCommandTarget(KeyInput keyInput)
        {
            var oleCommandData = OleCommandData.Empty;
            try
            {
                if (!TryConvertToOleCommandData(keyInput, out oleCommandData))
                {
                    return false;
                }

                if (!RunQueryStatus(oleCommandData))
                {
                    return false;
                }

                RunExec(oleCommandData);
                return true;
            }
            finally
            {
                oleCommandData.Dispose();
            }
        }

        /// <summary>
        /// Run QueryStatus and return whether or not it should be enabled 
        /// </summary>
        private bool RunQueryStatus(OleCommandData oleCommandData)
        {
            // First check and see if this represents a cached call to QueryStatus.  Visual Studio
            // will cache the result of QueryStatus for most types of commands that Vim will be 
            // interested in handling.  
            //
            // I haven't figured out the exact rules by which this cache is reset yet but it appears
            // to be when a QueryStatus / Exec pair executes succesfully or when Visual Studio loses
            // and gains focus again.  These may be related

            bool result;
            var commandId = oleCommandData.CommandId;
            if (_cachedQueryStatusMap.TryGetValue(commandId, out result))
            {
                return result;
            }

            result = RunQueryStatusCore(oleCommandData);
            _cachedQueryStatusMap[commandId] = result;
            return result;
        }

        /// <summary>
        /// Actually run the QueryStatus command and report the result
        /// </summary>
        private bool RunQueryStatusCore(OleCommandData oleCommandData)
        {
            OLECMD command;
            var hr = _commandTarget.QueryStatus(oleCommandData, out command);
            if (!ErrorHandler.Succeeded(hr))
            {
                return false;
            }

            // TODO: Visual Studio has slightly different behavior here IIRC.  I believe it will 
            // only cache if it's at least supported.  Need to check on that
            var result = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
            return result == (result & command.cmdf);
        }

        /// <summary>
        /// Run the Exec operation.  Make sure to properly manage the QueryStatus cache
        /// </summary>
        private void RunExec(OleCommandData oleCommandData)
        {
            var result = RunExecCore(oleCommandData);
            if (result)
            {
                _cachedQueryStatusMap.Clear();
            }
        }

        /// <summary>
        /// Exec the operation in question
        /// </summary>
        private bool RunExecCore(OleCommandData oleCommandData)
        {
            var variantOut = IntPtr.Zero;
            try
            {
                var hr = _commandTarget.Exec(oleCommandData);
                return ErrorHandler.Succeeded(hr);
            }
            finally
            {
                if (variantOut != IntPtr.Zero)
                {
                    NativeMethods.VariantClear(variantOut);
                    Marshal.FreeCoTaskMem(variantOut);
                }
            }
        }

        /// <summary>
        /// Try and convert the provided KeyInput value into OleCommandData.  This conversion is meant
        /// to simulate the standard converison of key input into OLE information in Visual Studio. This
        /// means we need to reproduce all of the behavior including not converting textual input
        /// here (unless it maps to a command).  Textual input typcially gets routed through WPF and 
        /// is routed to IOleCommandTarget in the default handler
        /// </summary>
        private bool TryConvertToOleCommandData(KeyInput keyInput, out OleCommandData oleCommandData)
        {
            if (keyInput.RawChar.IsSome())
            {
                if (Char.IsLetterOrDigit(keyInput.Char))
                {
                    oleCommandData = OleCommandData.Empty;
                    return false;
                }
            }

            return OleCommandUtil.TryConvert(keyInput, SimulateStandardKeyMappings, out oleCommandData);
        }
    }
}
