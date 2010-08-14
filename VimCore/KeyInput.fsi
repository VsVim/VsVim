
#light

namespace Vim

/// Virtual Key Codes are typed as int's
type VirtualKeyCode = int

[<Sealed>]
type KeyInput =

    /// The character representation of this input 
    member Char : char

    /// The VimKey for this KeyInput.  
    member Key : VimKey

    /// The modifier keys needed to produce this input
    member KeyModifiers : KeyModifiers

    /// Is the character for this KeyInput a digit
    member IsDigit : bool

    /// Determine if this a new line key.  Meant to match the Vim definition of <CR>
    member IsNewLine : bool

    /// Is this an arrow key?
    member IsArrowKey : bool 

    /// The empty KeyInput.  Used in places where a KeyInput is required but no 
    /// good mapping exists
    static member DefaultValue : KeyInput

    static member op_Equality : KeyInput * KeyInput -> bool
    static member op_Inequality : KeyInput * KeyInput -> bool

    interface System.IComparable 

module KeyInputUtil = 
    
    /// The set of core characters as a seq
    val CoreCharactersSet : Set<char>

    /// Try and convert a char to a virtualKey and ModifierKeys pair
    val TryCharToVirtualKeyAndModifiers : char -> (VirtualKeyCode * KeyModifiers) option

    /// Try and convert the given char to a KeyInput value
    val TryCharToKeyInput : char -> option<KeyInput>    

    /// Convert the virtual key code to a KeyInput value
    val VirtualKeyCodeToKeyInput : VirtualKeyCode -> KeyInput

    /// Convert the given virtual key and modifiers to a KeyInput
    val VirtualKeyCodeAndModifiersToKeyInput : VirtualKeyCode -> KeyModifiers -> KeyInput

    /// Try and change the key modifiers to the provided value.  This may 
    /// change the underlying Char value 
    val ChangeKeyModifiers : KeyInput -> KeyModifiers -> KeyInput

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

