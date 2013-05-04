using System;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.UI.Wpf.Implementation.Keyboard;
using Vim.UnitTest;

namespace Vim.UI.Wpf.UnitTest
{
    public class KeyProcessorSimulation
    {
        #region DefaultInputController

        /// <summary>
        /// Manages the routing of events to multiple KeyProcessor implementations
        /// </summary>
        private sealed class DefaultInputController
        {
            private readonly ITextView _textView;
            private readonly List<KeyProcessor> _keyProcessors;

            internal List<KeyProcessor> KeyProcessors
            {
                get { return _keyProcessors; }
            }

            internal DefaultInputController(ITextView textView)
            {
                _textView = textView;
                _keyProcessors = new List<KeyProcessor>();
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

        public sealed class DefaultKeyboardDevice : KeyboardDevice
        {
            /// <summary>
            /// Set of KeyModifiers which should be registered as down
            /// </summary>
            internal ModifierKeys DownKeyModifiers;

            internal DefaultKeyboardDevice()
                : this(InputManager.Current)
            {

            }

            internal DefaultKeyboardDevice(InputManager inputManager)
                : base(inputManager)
            {

            }

            protected override KeyStates GetKeyStatesFromSystem(Key key)
            {
                if (Key.LeftCtrl == key || Key.RightCtrl == key)
                {
                    return 0 != (DownKeyModifiers & ModifierKeys.Control) ? KeyStates.Down : KeyStates.None;
                }

                if (Key.LeftAlt == key || Key.RightAlt == key)
                {
                    return 0 != (DownKeyModifiers & ModifierKeys.Alt) ? KeyStates.Down : KeyStates.None;
                }

                if (Key.LeftShift == key || Key.RightShift == key)
                {
                    return 0 != (DownKeyModifiers & ModifierKeys.Shift) ? KeyStates.Down : KeyStates.None;
                }

                return KeyStates.None;
            }
        }

        #endregion

        private readonly DefaultKeyboardDevice _defaultKeyboardDevice;
        private readonly DefaultInputController _defaultInputController;
        private readonly Mock<PresentationSource> _presentationSource;
        private readonly IWpfTextView _wpfTextView;
        private readonly IVirtualKeyboard _virtualKeyboard;

        public KeyboardDevice KeyBoardDevice
        {
            get { return _defaultKeyboardDevice; }
        }

        public List<KeyProcessor> KeyProcessors
        {
            get { return _defaultInputController.KeyProcessors; }
        }

        public KeyProcessorSimulation(IWpfTextView wpfTextView)
        {
            _defaultInputController = new DefaultInputController(wpfTextView);
            _defaultKeyboardDevice = new DefaultKeyboardDevice(InputManager.Current);
            _wpfTextView = wpfTextView;
            _virtualKeyboard = new StandardVirtualKeyboard(NativeMethods.GetKeyboardLayout(0));

            Castle.DynamicProxy.Generators.AttributesToAvoidReplicating.Add(typeof(UIPermissionAttribute));
            _presentationSource = new Mock<PresentationSource>(MockBehavior.Strict);
        }

        public void Run(string text)
        {
            foreach (var cur in text)
            {
                var keyInput = KeyInputUtil.CharToKeyInput(cur);
                Run(keyInput);
            }
        }

        public void RunNotation(string notation)
        {
            foreach (var keyInput in KeyNotationUtil.StringToKeyInputSet(notation).KeyInputs)
            {
                Run(keyInput);
            }
        }

        public void Run(KeyInput keyInput)
        {
            Key key;
            ModifierKeys modifierKeys;

            if (!TryConvert(keyInput, out key, out modifierKeys))
            {
                throw new Exception(String.Format("Couldn't convert {0} to Wpf keys", keyInput));
            }

            try
            {
                _defaultKeyboardDevice.DownKeyModifiers = modifierKeys;

                if (PreProcess(keyInput, key, modifierKeys))
                {
                    return;
                }

                var text = keyInput.RawChar.IsSome()
                    ? keyInput.Char.ToString()
                    : String.Empty;
                Run(text, key, modifierKeys);
            }
            finally
            {
                _defaultKeyboardDevice.DownKeyModifiers = ModifierKeys.None;

            }
        }

        /// <summary>
        /// Run the KeyInput directly in the Wpf control
        /// </summary>
        private void Run(string text, Key key, ModifierKeys modifierKeys)
        {
            // First raise the KeyDown event
            var keyDownEventArgs = new KeyEventArgs(
                _defaultKeyboardDevice,
                _presentationSource.Object,
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
                CreateTextComposition(text));
            textInputEventArgs.RoutedEvent = UIElement.TextInputEvent;
            _defaultInputController.HandleTextInput(this, textInputEventArgs);

            var keyUpEventArgs = new KeyEventArgs(
                _defaultKeyboardDevice,
                _presentationSource.Object,
                0,
                key);
            keyUpEventArgs.RoutedEvent = UIElement.KeyUpEvent;
            _defaultInputController.HandleKeyUp(this, keyUpEventArgs);
        }

        private TextComposition CreateTextComposition(string text)
        {
            return _wpfTextView.VisualElement.CreateTextComposition(text);
        }

        private bool TryConvert(KeyInput keyInput, out Key key, out ModifierKeys modifierKeys)
        {
            if (AlternateKeyUtil.TrySpecialVimKeyToKey(keyInput.Key, out key))
            {
                var keyModifiers = keyInput.KeyModifiers;

                modifierKeys = ModifierKeys.None;
                if (KeyModifiers.Control == (keyModifiers & KeyModifiers.Control))
                {
                    modifierKeys |= ModifierKeys.Control;
                }

                if (KeyModifiers.Shift == (keyModifiers & KeyModifiers.Shift))
                {
                    modifierKeys |= ModifierKeys.Shift;
                }
                
                if (KeyModifiers.Alt == (keyModifiers & KeyModifiers.Alt))
                {
                    modifierKeys |= ModifierKeys.Alt;
                }

                return true;
            }

            if (keyInput.RawChar.IsSome())
            {
                uint virtualKey;
                VirtualKeyModifiers virtualKeyModifiers;

                if (_virtualKeyboard.TryMapChar(keyInput.Char, out virtualKey, out virtualKeyModifiers))
                {
                    key = KeyInterop.KeyFromVirtualKey((int)virtualKey);
                    modifierKeys = ModifierKeys.None;

                    if (VirtualKeyModifiers.Control == (virtualKeyModifiers & VirtualKeyModifiers.Control))
                    {
                        modifierKeys |= ModifierKeys.Control;
                    }

                    if (VirtualKeyModifiers.Shift == (virtualKeyModifiers & VirtualKeyModifiers.Shift))
                    {
                        modifierKeys |= ModifierKeys.Shift;
                    }

                    if (VirtualKeyModifiers.Alt == (virtualKeyModifiers & VirtualKeyModifiers.Alt))
                    {
                        modifierKeys |= ModifierKeys.Alt;
                    }

                    return true;
                }
            }

            key = Key.None;
            modifierKeys = ModifierKeys.None;
            return false;
        }

        private Key VimKeyToKey(VimKey vimKey)
        {
            switch (vimKey)
            {
                case VimKey.Escape:
                    return Key.Escape;
                case VimKey.Back:
                    return Key.Back;
                case VimKey.Up:
                    return Key.Up;
                case VimKey.Right:
                    return Key.Right;   
                case VimKey.Down:
                    return Key.Down;
                case VimKey.Left:
                    return Key.Left;
            }

            var keyInput = KeyInputUtil.VimKeyToKeyInput(vimKey);
            if (Char.IsLetter(keyInput.Char))
            {
                return StringToKey(keyInput.Char.ToString());
            }

            throw new Exception(String.Format("Can't convert {0} to a Wpf Key", vimKey));
        }

        private Key StringToKey(string str)
        {
            return (Key)Enum.Parse(typeof(Key), str);
        }

        protected virtual bool PreProcess(KeyInput keyInput, Key key, ModifierKeys modifierKeys)
        {
            return false;
        }
    }
}
