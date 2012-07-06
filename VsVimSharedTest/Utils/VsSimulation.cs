using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows;
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
using Vim.UnitTest;

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
        private sealed class DefaultCommandTarget : IOleCommandTarget
        {
            private readonly ITextView _textView;
            private readonly IEditorOperations _editorOperatins;

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
                    return VSConstants.E_FAIL;
                }

                return TryExec(editCommand.KeyInput) ? VSConstants.S_OK : VSConstants.E_FAIL;
            }

            int IOleCommandTarget.QueryStatus(ref Guid commandGroup, uint commandCount, OLECMD[] commands, IntPtr commandText)
            {
                EditCommand editCommand;
                if (1 != commandCount || !OleCommandUtil.TryConvert(commandGroup, commands[0].cmdID, commandText, KeyModifiers.None, out editCommand))
                {
                    commands[0].cmdf = 0;
                    return NativeMethods.S_OK;
                }

                commands[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                return NativeMethods.S_OK;
            }
        }

        #endregion

        #region ResharperCommandTarget

        /// <summary>
        /// Simulation of the R# command target.  This is intended to implement the most basic of 
        /// R# functionality for the purpose of testing
        /// </summary>
        private sealed class ResharperCommandTarget : IOleCommandTarget
        {
            private readonly ITextView _textView;
            private readonly IOleCommandTarget _nextCommandTarget;

            internal ResharperCommandTarget(ITextView textView, IOleCommandTarget nextCommandTarget)
            {
                _textView = textView;
                _nextCommandTarget = nextCommandTarget;
            }

            /// <summary>
            /// Try and simulate the execution of the few KeyInput values we care about
            /// </summary>
            private bool TryExec(KeyInput keyInput)
            {
                if (keyInput.Key == VimKey.Back)
                {
                    return TryExecBack();
                }

                return false;
            }

            /// <summary>
            /// R# will delete both parens when the Back key is used on the closing paren
            /// </summary>
            private bool TryExecBack()
            {
                var caretPoint = _textView.GetCaretPoint();
                if (caretPoint.Position < 2 ||
                    caretPoint.GetChar() != ')' ||
                    caretPoint.Subtract(1).GetChar() != '(')
                {
                    return false;
                }

                var span = new Span(caretPoint.Position - 1, 2);
                _textView.TextBuffer.Delete(span);
                return true;
            }

            int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
            {
                KeyInput keyInput;
                EditCommandKind editCommandKind;
                if (!OleCommandUtil.TryConvert(commandGroup, commandId, variantIn, out keyInput, out editCommandKind) ||
                    !TryExec(keyInput))
                {
                    return _nextCommandTarget.Exec(ref commandGroup, commandId, commandExecOpt, variantIn, variantOut);
                }

                return VSConstants.S_OK;
            }

            /// <summary>
            /// R# just forwards it's QueryStatus call onto the next target
            /// </summary>
            int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
            {
                return _nextCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
            }
        }

        #endregion

        #region DefaultInputController


        /// <summary>
        /// Manages the routing of events to multiple KeyProcessor implementations
        /// </summary>
        private sealed class DefaultInputController
        {
            private readonly ITextView _textView;
            private readonly List<Microsoft.VisualStudio.Text.Editor.KeyProcessor> _keyProcessors;

            internal DefaultInputController(ITextView textView, List<Microsoft.VisualStudio.Text.Editor.KeyProcessor> keyProcessors)
            {
                _textView = textView;
                _keyProcessors = keyProcessors;
            }

            /// <summary>
            /// Pass the event onto the various KeyProcessor values
            /// </summary>
            internal void HandleKeyUp(object sender, KeyEventArgs e)
            {
                foreach (var keyProcessor in _keyProcessors)
                {
                    if (e.Handled && !keyProcessor.IsInterestedInHandledEvents)
                    {
                        continue;
                    }

                    keyProcessor.KeyUp(e);
                }
            }

            /// <summary>
            /// Pass the event onto the various KeyProcessor values
            /// </summary>
            internal void HandleKeyDown(object sender, KeyEventArgs e)
            {
                foreach (var keyProcessor in _keyProcessors)
                {
                    if (e.Handled && !keyProcessor.IsInterestedInHandledEvents)
                    {
                        continue;
                    }

                    keyProcessor.KeyDown(e);
                }
            }

            /// <summary>
            /// Pass the event onto the various KeyProcessor values
            /// </summary>
            internal void HandleTextInput(object sender, TextCompositionEventArgs e)
            {
                foreach (var keyProcessor in _keyProcessors)
                {
                    if (e.Handled && !keyProcessor.IsInterestedInHandledEvents)
                    {
                        continue;
                    }

                    keyProcessor.TextInput(e);
                }
            }
        }

        #endregion

        #region DefaultKeyboardDevice

        private sealed class DefaultKeyboardDevice : KeyboardDevice
        {
            /// <summary>
            /// Set of KeyModifiers which should be registered as down
            /// </summary>
            internal KeyModifiers DownKeyModifiers;

            internal DefaultKeyboardDevice(InputManager inputManager)
                : base(inputManager)
            {

            }

            protected override KeyStates GetKeyStatesFromSystem(Key key)
            {
                if (Key.LeftCtrl == key || Key.RightCtrl == key)
                {
                    return 0 != (DownKeyModifiers & KeyModifiers.Control) ? KeyStates.Down : KeyStates.None;
                }

                if (Key.LeftAlt == key || Key.RightAlt == key)
                {
                    return 0 != (DownKeyModifiers & KeyModifiers.Alt) ? KeyStates.Down : KeyStates.None;
                }

                if (Key.LeftShift == key || Key.RightShift == key)
                {
                    return 0 != (DownKeyModifiers & KeyModifiers.Shift) ? KeyStates.Down : KeyStates.None;
                }

                return KeyStates.None;
            }
        }

        #endregion

        #region CommandKey

        /// <summary>
        /// Key for a command.  Used to cache query status information
        /// </summary>
        private struct CommandKey
        {
            internal readonly Guid CommandGroup;
            internal readonly uint CommandId;

            internal CommandKey(Guid commandGroup, uint commandId)
            {
                CommandGroup = commandGroup;
                CommandId = commandId;
            }
        }

        #endregion

        /// <summary>
        /// Cache of QueryStatus commands
        /// </summary>
        private readonly Dictionary<CommandKey, bool> _cachedQueryStatusMap = new Dictionary<CommandKey, bool>();

        /// <summary>
        /// Head of the IOleCommandTarget chain
        /// </summary>
        private readonly IOleCommandTarget _commandTarget;

        /// <summary>
        /// This is the default command target for Visual Studio.  It simulates the final command
        /// target on the chain.
        /// </summary>
        private readonly DefaultCommandTarget _defaultCommandTarget;

        private readonly IWpfTextView _wpfTextView;
        private readonly DefaultInputController _defaultInputController;
        private readonly DefaultKeyboardDevice _defaultKeyboardDevice;
        private readonly MockRepository _factory;
        private readonly Mock<IVsAdapter> _vsAdapter;
        private readonly Mock<IDisplayWindowBroker> _displayWindowBroker;
        private readonly Mock<IResharperUtil> _resharperUtil;
        private readonly TestableSynchronizationContext _testableSynchronizationContext;
        private bool _simulateStandardKeyMappings;

        internal bool SimulateStandardKeyMappings
        {
            get { return _simulateStandardKeyMappings; }
            set { _simulateStandardKeyMappings = value; }
        }

        internal Mock<IDisplayWindowBroker> DisplayWindowBroker
        {
            get { return _displayWindowBroker; }
        }

        internal VsSimulation(IVimBufferCoordinator bufferCoordinator, bool simulateResharper, bool simulateStandardKeyMappings, IEditorOperationsFactoryService editorOperationsFactoryService)
        {
            _wpfTextView = (IWpfTextView)bufferCoordinator.VimBuffer.TextView;
            _factory = new MockRepository(MockBehavior.Strict);
            _defaultKeyboardDevice = new DefaultKeyboardDevice(InputManager.Current);
            _testableSynchronizationContext = new TestableSynchronizationContext();
            _simulateStandardKeyMappings = simulateStandardKeyMappings;

            // Create the IVsAdapter and pick reasonable defaults here.  Consumers can modify 
            // this via an exposed property
            _vsAdapter = _factory.Create<IVsAdapter>();
            _vsAdapter.SetupGet(x => x.InAutomationFunction).Returns(false);
            _vsAdapter.SetupGet(x => x.KeyboardDevice).Returns(_defaultKeyboardDevice);
            _vsAdapter.Setup(x => x.IsReadOnly(It.IsAny<ITextBuffer>())).Returns(false);
            _vsAdapter.Setup(x => x.IsIncrementalSearchActive(_wpfTextView)).Returns(false);

            _resharperUtil = _factory.Create<IResharperUtil>();
            _resharperUtil.SetupGet(x => x.IsInstalled).Returns(simulateResharper);

            _displayWindowBroker = _factory.Create<IDisplayWindowBroker>();
            _displayWindowBroker.SetupGet(x => x.IsCompletionActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsQuickInfoActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsSignatureHelpActive).Returns(false);
            _displayWindowBroker.SetupGet(x => x.IsSmartTagSessionActive).Returns(false);

            _defaultCommandTarget = new DefaultCommandTarget(
                bufferCoordinator.VimBuffer.TextView,
                editorOperationsFactoryService.GetEditorOperations(bufferCoordinator.VimBuffer.TextView));

            // Create the VsCommandTarget.  It's next is the final and default Visual Studio 
            // command target
            var vsTextView = _factory.Create<IVsTextView>();
            IOleCommandTarget nextCommandTarget = _defaultCommandTarget;
            vsTextView.Setup(x => x.AddCommandFilter(It.IsAny<IOleCommandTarget>(), out nextCommandTarget)).Returns(VSConstants.S_OK);
            var vsCommandTarget = VsCommandTarget.Create(
                bufferCoordinator,
                vsTextView.Object,
                _vsAdapter.Object,
                _displayWindowBroker.Object,
                _resharperUtil.Object).Value;

            // Time to setup the start command target.  If we are simulating R# then put them ahead of VsVim
            // on the IOleCommandTarget chain.  VsVim doesn't try to fight R# and prefers instead to be 
            // behind them
            if (simulateResharper)
            {
                var resharperCommandTarget = new ResharperCommandTarget(_wpfTextView, vsCommandTarget);
                _commandTarget = resharperCommandTarget;
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
            var keyProcessors = new List<Microsoft.VisualStudio.Text.Editor.KeyProcessor>();
            keyProcessors.Add(new VsKeyProcessor(_vsAdapter.Object, bufferCoordinator));
            keyProcessors.Add(new SimulationKeyProcessor(bufferCoordinator.VimBuffer.TextView));
            _defaultInputController = new DefaultInputController(bufferCoordinator.VimBuffer.TextView, keyProcessors);
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
        /// <param name="c"></param>
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
                // Update the KeyModifiers while this is being processed by our key processor
                var keyModifiers = keyInput.KeyModifiers;

                // The KeyInput for an upper letter doesn't have a Shift modifier.  However the 
                // keyboard state which produced the letter will likely have it (not sure about 
                // caps lock).  Need to simulate that here
                if (Char.IsLetter(keyInput.Char) && Char.IsUpper(keyInput.Char))
                {
                    keyModifiers |= KeyModifiers.Shift;
                }

                _defaultKeyboardDevice.DownKeyModifiers = keyInput.KeyModifiers;

                if (!RunInOleCommandTarget(keyInput))
                {
                    RunInWpf(keyInput);
                }

                _testableSynchronizationContext.RunAll();
            }
            finally
            {
                _defaultKeyboardDevice.DownKeyModifiers = KeyModifiers.None;
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
            var key = new CommandKey(oleCommandData.CommandGroup, oleCommandData.CommandId);
            if (_cachedQueryStatusMap.TryGetValue(key, out result))
            {
                return result;
            }

            result = RunQueryStatusCore(oleCommandData);
            _cachedQueryStatusMap[key] = result;
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
        /// Run the KeyInput directly in the Wpf control
        /// </summary>
        private void RunInWpf(KeyInput keyInput)
        {
            Castle.DynamicProxy.Generators.AttributesToAvoidReplicating.Add(typeof(UIPermissionAttribute));
            var presentationSource = _factory.Create<PresentationSource>();

            // Normalize upper case letters here to lower and add the shift key modifier if
            // necessary
            if (Char.IsUpper(keyInput.Char))
            {
                var lowerKeyInput = KeyInputUtil.CharToKeyInput(Char.ToLower(keyInput.Char));
                keyInput = KeyInputUtil.ChangeKeyModifiersDangerous(lowerKeyInput, keyInput.KeyModifiers | KeyModifiers.Shift);
            }

            Key key;
            if (!KeyUtil.TryConvertToKeyOnly(keyInput.Key, out key))
            {
                throw new Exception("Couldn't get the WPF key for the given KeyInput");
            }

            // First raise the KeyDown event
            var keyDownEventArgs = new KeyEventArgs(
                _defaultKeyboardDevice,
                presentationSource.Object,
                0,
                key);
            keyDownEventArgs.RoutedEvent = UIElement.KeyDownEvent;
            _defaultInputController.HandleKeyDown(this, keyDownEventArgs);

            // If the event is handled then return
            if (keyDownEventArgs.Handled)
            {
                return;
            }

            // Now raise the TextInput event
            var textInputEventArgs = new TextCompositionEventArgs(
                _defaultKeyboardDevice,
                new TextComposition(InputManager.Current, _wpfTextView.VisualElement, keyInput.Char.ToString()));
            textInputEventArgs.RoutedEvent = UIElement.TextInputEvent;
            _defaultInputController.HandleTextInput(this, textInputEventArgs);

            var keyUpEventArgs = new KeyEventArgs(
                _defaultKeyboardDevice,
                presentationSource.Object,
                0,
                key);
            keyUpEventArgs.RoutedEvent = UIElement.KeyUpEvent;
            _defaultInputController.HandleKeyUp(this, keyUpEventArgs);
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
