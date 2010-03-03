#light

namespace Vim
open System.Windows.Input


module InputUtil = 

    /// Set of predefined KeyInput values
    val KeyInputList : KeyInput list 
    val KeyToKeyInput : Key -> KeyInput
    val KeyToChar : Key -> char
    val KeyInputToChar : KeyInput -> char
    val TryCharToKeyInput : char -> option<KeyInput>    
    val CharToKeyInput : char -> KeyInput
    val KeyAndModifierToKeyInput : Key -> ModifierKeys -> KeyInput

    /// Set the modifier keys on the specified KeyInput
    val SetModifiers : ModifierKeys -> KeyInput -> KeyInput

