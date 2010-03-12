#light

namespace Vim
open System.Windows.Input


module InputUtil = 

    /// Try and convert a char to a virtualKey and ModifierKeys pair
    val TryCharToVirtualKeyAndModifiers : char -> (int * KeyModifiers) option

    /// Try and convert the given char to a KeyInput value
    val TryCharToKeyInput : char -> option<KeyInput>    

    /// Try and convert the given virtual key to a char
    val TryVirtualKeyCodeToChar : int -> option<char>

    /// Convert the specified Virtual Key code to a KeyInput 
    val WellKnownKeyToKeyInput : WellKnownKey -> KeyInput

    /// Convert the passed in char into a KeyInput value
    val CharToKeyInput : char -> KeyInput

    /// Set the modifier keys on the specified KeyInput
    val SetModifiers : KeyModifiers -> KeyInput -> KeyInput

