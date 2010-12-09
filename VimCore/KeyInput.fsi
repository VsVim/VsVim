
#light

namespace Vim

/// Virtual Key Codes are typed as int's
type VirtualKeyCode = int

/// Represents a key input by the user.  This mapping is independent of keyboard 
/// layout and simply represents Vim's view of key input
[<Sealed>]
type KeyInput =

    /// The character representation of this input.  If there is no character representation
    /// then Char.MinValue will be returend
    member Char : char

    /// Raw character for this Key Input
    member RawChar : char option

    /// The VimKey for this KeyInput.  
    member Key : VimKey

    /// The extra modifier keys applied to the VimKey value
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
    interface System.IComparable<KeyInput>
    interface System.IEquatable<KeyInput>

module KeyInputUtil = 
    
    /// The set of core characters as a seq
    val CoreCharacterList : char list

    /// The core set of KeyInput values that Vim is concerned with.  With the exception of
    /// upper case letters this doesn't include any modifiers.  
    val CoreKeyInputList : KeyInput list

    /// Try and convert the given char to a KeyInput value
    val CharToKeyInput : char -> KeyInput

    /// Convert the passed in char and modifiers into a KeyInput value
    val CharWithControlToKeyInput : char -> KeyInput

    /// Convert the passed in char and modifiers into a KeyInput value
    val CharWithAltToKeyInput : char -> KeyInput

    /// Convert the specified VimKey code to a KeyInput 
    val VimKeyToKeyInput : VimKey -> KeyInput

    /// Convert the specified VimKey to a KeyInput with the given KeyModifiers
    val VimKeyAndModifiersToKeyInput : VimKey -> KeyModifiers -> KeyInput

    /// Change the KeyModifiers associated with this KeyInput.  Will not change the value
    /// of the underlying char.  Although it may produce a KeyInput that makes no 
    /// sense.  For example it's very possible to have KeyInput('a', KeyModifiers.Shift) but
    /// it will be extremely hard to produce that in a keyboard.  This seems odd at first 
    /// but it's a scenario that Vim supports (or doesn't support depending on how you 
    val ChangeKeyModifiers : KeyInput -> KeyModifiers -> KeyInput


