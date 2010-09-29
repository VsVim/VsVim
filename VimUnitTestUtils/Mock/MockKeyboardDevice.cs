using System.Windows.Input;

namespace Vim.UnitTest.Mock
{
    public class MockKeyboardDevice : KeyboardDevice
    {
        public ModifierKeys ModifierKeysImpl;

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
    }
}
