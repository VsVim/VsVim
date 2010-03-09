#light

namespace Vim
open System.Windows.Input


module InputUtil = 

    /// Try and convert the given char to a KeyInput value
    val TryCharToKeyInput : char -> option<KeyInput>    

    /// Try and convert the given Key into a KeyInput value
    val TryKeyToKeyInput : Key -> option<KeyInput>

    /// Convert the passed in char into a KeyInput value
    val CharToKeyInput : char -> KeyInput

    /// Convert the passed in Key to a KeyInput value.  If there is no direct translation
    /// of the Key, Char.MinValue will be used as the char
    val KeyToKeyInput : Key -> KeyInput

    /// Convert the specified Key and ModifierKeys into a KeyInput value
    val KeyAndModifierToKeyInput : Key -> ModifierKeys -> KeyInput

    /// Set the modifier keys on the specified KeyInput
    val SetModifiers : ModifierKeys -> KeyInput -> KeyInput

