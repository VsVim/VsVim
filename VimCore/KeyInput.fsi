
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

    /// Does this represent a character only.  I.E. a backing char with no modifiers
    member IsCharOnly : bool

    /// The VimKey for this KeyInput.  
    member Key : VimKey

    /// The extra modifier keys applied to the VimKey value
    member KeyModifiers : KeyModifiers

    /// Is the character for this KeyInput a digit
    member IsDigit : bool

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

    /// The Escape / <Esc> / <C-[> Key
    val EscapeKey : KeyInput 

    /// The Tab / <Tab> / <C-I> Key
    val TabKey : KeyInput 
    
    /// The LineFeed / <NL> / <C-J> key
    val LineFeedKey : KeyInput

    /// The Enter / <CR> / <Enter> / <C-M> / <Return>
    val EnterKey : KeyInput

    /// The set of special keys which have multiple real aliases back at a single
    /// Key
    val SpecialKeyInputList : KeyInput list

    /// The KeyInput for every VimKey in the system (except Unknown)
    val VimKeyInputList : KeyInput list

    /// The set of core characters as a seq
    val VimKeyCharList : char list

    /// The core set of KeyInput values that Vim is concerned with.  This includes all of the
    /// VimKey entries and Special KeyInput values
    val AllKeyInputList : KeyInput list

    /// Try and convert the given char to a KeyInput value
    val CharToKeyInput : char -> KeyInput

    /// Convert the passed in char to a KeyInput with Control
    val CharWithControlToKeyInput : char -> KeyInput

    /// Convert the passed in char to a KeyInput with Alt
    val CharWithAltToKeyInput : char -> KeyInput

    /// Convert the passed in char to a KeyInput with Shift
    val CharWithShiftToKeyInput : char -> KeyInput

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

