#if VS_UNIT_TEST_HOST
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Vim.UnitTest
{
    public sealed class MockKeyboardDevice : KeyboardDevice
    {
        private sealed class MockPresentationSource : PresentationSource
        {
            private Visual _rootVisual;

            protected override CompositionTarget GetCompositionTargetCore()
            {
                throw new NotImplementedException();
            }

            public override bool IsDisposed
            {
                get { return false; }
            }

            public override Visual RootVisual
            {
                get { return _rootVisual; }
                set { _rootVisual = value; }
            }
        }

        private static readonly RoutedEvent s_testEvent = EventManager.RegisterRoutedEvent(
                "Test Event",
                RoutingStrategy.Bubble,
                typeof(MockKeyboardDevice),
                typeof(MockKeyboardDevice));

        public ModifierKeys ModifierKeysImpl;

        public MockKeyboardDevice()
            : this(InputManager.Current)
        {
        }

        public MockKeyboardDevice(InputManager manager)
            : base(manager)
        {
        }

        protected override KeyStates GetKeyStatesFromSystem(Key key)
        {
            var hasMod = false;
            switch (key)
            {
                case Key.LeftAlt:
                case Key.RightAlt:
                    hasMod = HasModifierKey(ModifierKeys.Alt);
                    break;
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    hasMod = HasModifierKey(ModifierKeys.Control);
                    break;
                case Key.LeftShift:
                case Key.RightShift:
                    hasMod = HasModifierKey(ModifierKeys.Shift);
                    break;
            }

            return hasMod ? KeyStates.Down : KeyStates.None;
        }

        public KeyEventArgs CreateKeyEventArgs(
            Key key,
            ModifierKeys modKeys = ModifierKeys.None)
        {
            var arg = new KeyEventArgs(
                this,
                new MockPresentationSource(),
                0,
                key);
            ModifierKeysImpl = modKeys;
            arg.RoutedEvent = s_testEvent;
            return arg;
        }

        public KeyEventArgs CreateKeyEventArgs(KeyInput keyInput)
        {
            if (!TryGetKeyForKeyInput(keyInput, out Key key, out ModifierKeys modKeys))
            {
                throw new Exception();
            }

            return CreateKeyEventArgs(key, modKeys);
        }

        public bool TryGetKeyForKeyInput(KeyInput keyInput, out Key key, out ModifierKeys modKeys)
        {
            key = Key.Delete;
            modKeys = ModifierKeys.None;

            // At this time we don't support any use of modifier keys here 
            if (keyInput.KeyModifiers != VimKeyModifiers.None)
            {
                return false;
            }

            modKeys = ModifierKeys.None;
            switch (keyInput.Key)
            {
                case VimKey.Tab:
                    key = Key.Tab;
                    return true;
                case VimKey.Left:
                    key = Key.Left;
                    return true;
                case VimKey.Right:
                    key = Key.Right;
                    return true;
                case VimKey.Up:
                    key = Key.Up;
                    return true;
                case VimKey.Down:
                    key = Key.Down;
                    return true;
                case VimKey.Escape:
                    key = Key.Escape;
                    return true;
                case VimKey.Enter:
                    key = Key.Enter;
                    return true;
                case VimKey.Delete:
                    key = Key.Delete;
                    return true;
                case VimKey.Back:
                    key = Key.Back;
                    return true;
                case VimKey.Home:
                    key = Key.Home;
                    return true;
                case VimKey.End:
                    key = Key.End;
                    return true;
                case VimKey.LineFeed:
                    key = Key.J;
                    modKeys = ModifierKeys.Control;
                    return true;
            }

            if (char.IsLetter(keyInput.Char))
            {
                var c = char.ToLower(keyInput.Char);
                key = (Key)((c - 'a') + (int)Key.A);
                modKeys = char.IsUpper(keyInput.Char) ? ModifierKeys.Shift : ModifierKeys.None;
                return true;
            }

            if (keyInput.Char == ' ')
            {
                key = Key.Space;
                return true;
            }

            var rawChar = (int)keyInput.Char;
            if (rawChar >= 1 && rawChar <= 26)
            {
                key = (Key)(Key.A + (rawChar - 1));
                modKeys = ModifierKeys.Control;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Simulate the given KeyInput being sent to the given UIElement 
        /// </summary>
        public void SendKeyStroke(UIElement target, KeyInput keyInput)
        {
            if (!TryGetKeyForKeyInput(keyInput, out Key key, out ModifierKeys modKeys))
            {
                throw new Exception();
            }

            ModifierKeysImpl = modKeys;
            try
            {
                // First step is preview key down 
                var keyEventArgs = new KeyEventArgs(
                    this,
                    new MockPresentationSource(),
                    0,
                    key);

                if (!RaiseEvents(target, keyEventArgs, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent))
                {
                    var raiseUp = true;
                    if (char.IsLetterOrDigit(keyInput.Char))
                    {
                        var textComposition = new TextComposition(InputManager.Current, target, keyInput.Char.ToString());
                        var textCompositionEventArgs = new TextCompositionEventArgs(this, textComposition);
                        raiseUp = !RaiseEvents(target, textCompositionEventArgs, TextCompositionManager.TextInputEvent);
                    }

                    if (raiseUp)
                    {
                        RaiseEvents(target, keyEventArgs, Keyboard.PreviewKeyUpEvent, Keyboard.KeyUpEvent);
                    }
                }
            }
            finally
            {
                ModifierKeysImpl = ModifierKeys.None;
            }
        }

        private bool RaiseEvents(UIElement target, RoutedEventArgs e, params RoutedEvent[] routedEventArray)
        {
            foreach (var routedEvent in routedEventArray)
            {
                e.RoutedEvent = routedEvent;
                target.RaiseEvent(e);
                if (e.Handled)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasModifierKey(ModifierKeys modKey)
        {
            return 0 != (ModifierKeysImpl & modKey);
        }
    }
}
#endif
