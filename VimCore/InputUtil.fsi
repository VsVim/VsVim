#light

namespace Vim


module InputUtil = 
    
    /// The core set of characters Vim considers for input
    val CoreCharacters : char seq

    /// The set of core characters as a seq
    val CoreCharactersSet : Set<char>

    /// Try and convert a char to a virtualKey and ModifierKeys pair
    val TryCharToVirtualKeyAndModifiers : char -> (int * KeyModifiers) option

    /// Try and convert the given char to a KeyInput value
    val TryCharToKeyInput : char -> option<KeyInput>    

    /// Try and convert the given virtual key to a char
    val TryVirtualKeyCodeToKeyInput : int -> option<KeyInput>

    /// Convert the specified VimKey code to a KeyInput 
    val VimKeyToKeyInput : VimKey -> KeyInput

    /// Convert the specified VimKey to a KeyInput with the given KeyModifiers
    val VimKeyAndModifiersToKeyInput : VimKey -> KeyModifiers -> KeyInput

    /// Convert the passed in char into a KeyInput value
    val CharToKeyInput : char -> KeyInput

    /// Convert the passed in char and modifiers into a KeyInput value
    val CharAndModifiersToKeyInput : char -> KeyModifiers -> KeyInput

    /// Set the modifier keys on the specified KeyInput
    val SetModifiers : KeyModifiers -> KeyInput -> KeyInput

