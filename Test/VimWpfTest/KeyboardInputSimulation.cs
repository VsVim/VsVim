using System;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.UI.Wpf.Implementation.Misc;
using Vim.UnitTest;

namespace Vim.UI.Wpf.UnitTest
{
    public enum KeyDirection
    {
        Up,
        Down
    }

    /// <summary>
    /// This type is used to simulate how keyboard input navigates through WPF events
    /// </summary>
    public class KeyboardInputSimulation
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
            internal void HandlePreviewKeyDown(object sender, KeyEventArgs e)
            {
                for (int i = _keyProcessors.Count - 1; i >=0 ; i--)
                {
                    var keyProcessor = _keyProcessors[i];
                    if (e.Handled && !keyProcessor.IsInterestedInHandledEvents)
                    {
                        continue;
                    }

                    keyProcessor.PreviewKeyDown(e);
                }
            }

            /// <summary>
            /// Pass the event onto the various KeyProcessor values
            /// </summary>
            internal void HandlePreviewKeyUp(object sender, KeyEventArgs e)
            {
                for (int i = _keyProcessors.Count - 1; i >=0 ; i--)
                {
                    var keyProcessor = _keyProcessors[i];
                    if (e.Handled && !keyProcessor.IsInterestedInHandledEvents)
                    {
                        continue;
                    }

                    keyProcessor.PreviewKeyUp(e);
                }
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

        #region KeyData

        private struct KeyData
        {
            internal readonly Key Key;
            internal readonly ModifierKeys ModifierKeys;
            internal KeyData(Key key, ModifierKeys modifierKeys)
            {
                Key = key;
                ModifierKeys = modifierKeys;
            }
        }

        #endregion

        private static readonly Dictionary<KeyInput, KeyData> KeyDataMap;

        static KeyboardInputSimulation()
        {
            var combos = new[]
                {
                    ModifierKeys.None,
                    ModifierKeys.Shift,
                    ModifierKeys.Control,
                    ModifierKeys.Alt,
                    ModifierKeys.Alt | ModifierKeys.Shift
                };

            var map = new Dictionary<KeyInput, KeyData>();
            foreach (char c in KeyInputUtilTest.CharLettersLower)
            {
                foreach (var mod in combos)
                {
                    var keyMod = AlternateKeyUtil.ConvertToKeyModifiers(mod);
                    var keyInput = KeyInputUtil.ApplyModifiersToChar(c, keyMod);
                    var key = (Key)((c - 'a') + (int)Key.A);
                    map[keyInput] = new KeyData(key, mod);
                }
            }

            map[KeyInputUtil.CharToKeyInput(' ')] = new KeyData(Key.Space, ModifierKeys.None);
            map[KeyInputUtil.CharToKeyInput('.')] = new KeyData(Key.OemPeriod, ModifierKeys.None);
            map[KeyInputUtil.CharToKeyInput(';')] = new KeyData(Key.OemSemicolon, ModifierKeys.None);
            map[KeyInputUtil.CharToKeyInput(':')] = new KeyData(Key.OemSemicolon, ModifierKeys.Shift);

            KeyDataMap = map;
        }

        private readonly DefaultKeyboardDevice _defaultKeyboardDevice;
        private readonly DefaultInputController _defaultInputController;
        private readonly Mock<PresentationSource> _presentationSource;
        private readonly IWpfTextView _wpfTextView;

        public KeyboardDevice KeyBoardDevice
        {
            get { return _defaultKeyboardDevice; }
        }

        public List<KeyProcessor> KeyProcessors
        {
            get { return _defaultInputController.KeyProcessors; }
        }

        public KeyboardInputSimulation(IWpfTextView wpfTextView)
        {
            _defaultInputController = new DefaultInputController(wpfTextView);
            _defaultKeyboardDevice = new DefaultKeyboardDevice(InputManager.Current);
            _wpfTextView = wpfTextView;

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

        /// <summary>
        /// This method will simulate a key sequence where the provided keyInput value is pressed
        /// down and released 
        /// </summary>
        public void Run(KeyInput keyInput)
        {
            Key key;
            ModifierKeys modifierKeys;

            if (!TryConvert(keyInput, out key, out modifierKeys))
            {
                throw new Exception(String.Format("Couldn't convert '{0}' to Wpf keys", keyInput));
            }

            try
            {
                _defaultKeyboardDevice.DownKeyModifiers = modifierKeys;
                var text = keyInput.RawChar.IsSome()
                    ? keyInput.Char.ToString()
                    : String.Empty;
                Run(text, keyInput, key, modifierKeys);
            }
            finally
            {
                _defaultKeyboardDevice.DownKeyModifiers = ModifierKeys.None;
            }
        }

        /// <summary>
        /// Run the KeyInput directly in the Wpf control
        /// </summary>
        private void Run(string text, KeyInput keyInput, Key key, ModifierKeys modifierKeys)
        {
            if (!RunDown(keyInput, key, modifierKeys))
            {
                // If the down event wasn't handled then we should proceed to actually providing
                // textual composition 
                var textInputEventArgs = new TextCompositionEventArgs(
                    _defaultKeyboardDevice,
                    CreateTextComposition(text));
                textInputEventArgs.RoutedEvent = UIElement.TextInputEvent;
                _defaultInputController.HandleTextInput(this, textInputEventArgs);
            }

            // The key up code is processed even if the down or text composition is handled by 
            // the calling code.  It's an independent event 
            RunUp(keyInput, key, modifierKeys);
        }

        private bool RunDown(KeyInput keyInput, Key key, ModifierKeys modifierKeys)
        {
            // First let the preprocess step have a chance to intercept the key
            if (PreProcess(KeyDirection.Down, keyInput, key, modifierKeys))
            {
                return true;
            }

            // Next raise the preview event
            // First raise the PreviewKeyDown event
            var previewKeyDownEventArgs = CreateKeyEventArgs(key, UIElement.PreviewKeyDownEvent);
            _defaultInputController.HandlePreviewKeyDown(this, previewKeyDownEventArgs);
            if (previewKeyDownEventArgs.Handled)
            {
                return true;
            }

            // If the preview event wasn't handled then move to the down event 
            var keyDownEventArgs = CreateKeyEventArgs(key, UIElement.KeyDownEvent);
            _defaultInputController.HandleKeyDown(this, keyDownEventArgs);
            return keyDownEventArgs.Handled;
        }

        private bool RunUp(KeyInput keyInput, Key key, ModifierKeys modifierKeys)
        {
            // First let the preprocess step have a chance to intercept the key
            if (PreProcess(KeyDirection.Up, keyInput, key, modifierKeys))
            {
                return true;
            }

            // Now move onto the up style events.  These are raised even if the chain of down events
            // are handled 
            var previewKeyUpEventArgs = CreateKeyEventArgs(key, UIElement.PreviewKeyUpEvent);
            _defaultInputController.HandlePreviewKeyUp(this, previewKeyUpEventArgs);
            if (previewKeyUpEventArgs.Handled)
            {
                return true;
            }

            var keyUpEventArgs = CreateKeyEventArgs(key, UIElement.KeyUpEvent);
            _defaultInputController.HandleKeyUp(this, keyUpEventArgs);
            return keyUpEventArgs.Handled;
        }

        private TextComposition CreateTextComposition(string text)
        {
            return _wpfTextView.VisualElement.CreateTextComposition(text);
        }

        private KeyEventArgs CreateKeyEventArgs(Key key, RoutedEvent e)
        {
            var keyEventArgs = new KeyEventArgs(
                _defaultKeyboardDevice,
                _presentationSource.Object,
                0,
                key);
            keyEventArgs.RoutedEvent = e;
            return keyEventArgs;
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

            KeyData keyData;
            if (KeyDataMap.TryGetValue(keyInput, out keyData))
            {
                key = keyData.Key;
                modifierKeys = keyData.ModifierKeys;
                return true;
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

        /// <summary>
        /// This is intended to simulate the pre-processing of input similar to pre-process message.  This will
        /// return true if the event was handled
        /// </summary>
        protected virtual bool PreProcess(KeyDirection keyDirection, KeyInput keyInput, Key key, ModifierKeys modifierKeys)
        {
            return false;
        }
    }
}
