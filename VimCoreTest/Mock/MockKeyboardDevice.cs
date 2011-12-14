using System.Windows;
using System.Windows.Input;

namespace Vim.UnitTest.Mock
{
    public class MockKeyboardDevice : KeyboardDevice
    {
        private static RoutedEvent s_testEvent = EventManager.RegisterRoutedEvent(
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

        private bool HasModifierKey(ModifierKeys modKey)
        {
            return 0 != (ModifierKeysImpl & modKey);
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
    }
}
