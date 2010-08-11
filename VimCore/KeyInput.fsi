
#light

namespace Vim

/// Virtual Key Codes are typed as int's
type VirtualKeyCode = int

[<Sealed>]
type KeyInput =

    new : VirtualKeyCode * VimKey * KeyModifiers * char -> KeyInput

    /// The virtual key code for the KeyInput
    member VirtualKeyCode : VirtualKeyCode

    /// The character representation of this input 
    member Char : char

    /// The VimKey for this KeyInput.  
    member Key : VimKey

    /// The modifier keys needed to produce this input
    member KeyModifiers : KeyModifiers

    /// Does this have the shift modifier?
    member HasShiftModifier : bool

    /// Is the character for this KeyInput a digit
    member IsDigit : bool

    /// Determine if this a new line key.  Meant to match the Vim definition of <CR>
    member IsNewLine : bool

    /// Is this an arrow key?
    member IsArrowKey : bool 

    static member op_Equality : KeyInput * KeyInput -> bool
    static member op_Inequality : KeyInput * KeyInput -> bool

    interface System.IComparable 

module InputUtil = 
    
    /// The core set of characters Vim considers for input
    val CoreCharacters : char seq

    /// The set of core characters as a seq
    val CoreCharactersSet : Set<char>

    /// Try and convert a char to a virtualKey and ModifierKeys pair
    val TryCharToVirtualKeyAndModifiers : char -> (VirtualKeyCode * KeyModifiers) option

    /// Try and convert the given char to a KeyInput value
    val TryCharToKeyInput : char -> option<KeyInput>    

    /// Try and convert the given virtual key to a char
    val TryVirtualKeyCodeToKeyInput : VirtualKeyCode -> option<KeyInput>

    /// Convert the specified VimKey code to a KeyInput 
    val VimKeyToKeyInput : VimKey -> KeyInput

    /// Convert the specified VimKey to a KeyInput with the given KeyModifiers
    val VimKeyAndModifiersToKeyInput : VimKey -> KeyModifiers -> KeyInput

    /// Convert the passed in char into a KeyInput value
    val CharToKeyInput : char -> KeyInput

    /// Convert the passed in char and modifiers into a KeyInput value
    val CharWithControlToKeyInput : char -> KeyInput

    /// Convert the passed in char and modifiers into a KeyInput value
    val CharWithAltToKeyInput : char -> KeyInput

