#light

namespace Vim
open System.Windows.Input


module InputUtil = 
    val KeyInputList : KeyInput list 
    val KeyToKeyInput : Key -> KeyInput
    val KeyToChar : Key -> char
    val KeyInputToChar : KeyInput -> char
    val TryCharToKeyInput : char -> option<KeyInput>    
    val CharToKeyInput : char -> KeyInput
    val KeyAndModifierToKeyInput : Key -> ModifierKeys -> KeyInput
