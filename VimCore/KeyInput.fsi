
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

    /// Returns the actual backing character in the form of an option.  Several keys 
    /// don't have corresponding char values and will return None
    member RawChar : char option

    /// The VimKey for this KeyInput.  
    member Key : VimKey

    /// The extra modifier keys applied to the VimKey value
    member KeyModifiers : KeyModifiers

    /// Is the character for this KeyInput a digit
    member IsDigit : bool

    /// Is this an arrow key?
    member IsArrowKey : bool 

    /// Is this a function key
    member IsFunctionKey : bool

    /// The empty KeyInput.  Used in places where a KeyInput is required but no 
    /// good mapping exists
    static member DefaultValue : KeyInput

    static member op_Equality : KeyInput * KeyInput -> bool
    static member op_Inequality : KeyInput * KeyInput -> bool

    interface System.IComparable
    interface System.IComparable<KeyInput>
    interface System.IEquatable<KeyInput>

module KeyInputUtil = 

    /// The Alternate Null Key : <C-@>
    val AlternateNullKeyInput : KeyInput

    /// The Alternate Backspace Key : <C-H>
    val AlternateBackspaceKeyInput : KeyInput

    /// The Alternate Tab Key <C-i>
    val AlternateTabKey : KeyInput 

    /// The alternate LineFeed key: <C-j>
    val AlternateLineFeedKey : KeyInput

    /// The alternate FormFeed key: <C-L>
    val AlternateFormFeedKey : KeyInput

    /// The Alternate Enter Key : <C-m>
    val AlternateEnterKey : KeyInput

    /// The Alternate Escape Key: <C-[>
    val AlternateEscapeKey : KeyInput

    /// The set of special keys which are alias's back into core VimKey values
    val AlternateKeyInputList : KeyInput list

    /// The Null Key: VimKey.Null
    val NullKey : KeyInput

    /// The Backspace Key: VimKey.Back
    val BackspaceKey : KeyInput

    /// The LineFeed key: VimKey.LineFeed
    val LineFeedKey : KeyInput

    /// The FormFeed key: VimKey.FormFeed
    val FormFeedKey : KeyInput

    /// The Enter Key: VimKey.Enter
    val EnterKey : KeyInput

    /// The Escape Key: VimKey.Escape
    val EscapeKey : KeyInput 

    /// The Tab Key: VimKey.Tab
    val TabKey : KeyInput 

    /// The KeyInput for every VimKey in the system (except Unknown).  This will not
    /// include any alternate KeyInput values
    val VimKeyInputList : KeyInput list

    /// The set of core characters as a seq
    val VimKeyCharList : char list

    /// Apply the modifiers to the given KeyInput and determine the result.  This will
    /// not necessarily return a KeyInput with the modifier set.  It attempts to unify 
    /// certain ambiguous combinations.
    val ApplyModifiers : keyInput : KeyInput -> modifiers : KeyModifiers -> KeyInput

    /// Apply the modifiers to the given VimKey
    val ApplyModifiersToVimKey : VimKey -> modifiers : KeyModifiers -> KeyInput

    /// Try and convert the given char to a KeyInput value
    val CharToKeyInput : char -> KeyInput

    /// Convert the passed in char to a KeyInput with Control
    val CharWithControlToKeyInput : char -> KeyInput

    /// Convert the passed in char to a KeyInput with Alt
    val CharWithAltToKeyInput : char -> KeyInput

    /// Convert the specified VimKey code to a KeyInput 
    val VimKeyToKeyInput : VimKey -> KeyInput

    /// Change the KeyModifiers associated with this KeyInput.  Will not change the value
    /// of the underlying char.  Although it may produce a KeyInput that makes no 
    /// sense.  For example it's very possible to have KeyInput('a', KeyModifiers.Shift) but
    /// it will be extremely hard to produce that in a keyboard (if possible at all).  
    ///
    /// This method should be avoided.  If you need to apply modifiers then use
    /// ApplyModifiers which uses Vim semantics when deciding how to apply the modifiers
    val ChangeKeyModifiersDangerous : KeyInput -> KeyModifiers -> KeyInput

    /// Given a KeyInput value which is an Alternate KeyInput return the value it as an
    /// alternate for
    val GetAlternate : KeyInput -> KeyInput option

    /// Given an alternate KeyInput get the primary KeyInput it's an alternate for
    val GetPrimary : KeyInput -> KeyInput option

    /// Get the alternate key for the given KeyInput if it's a key from the keypad 
    val GetNonKeypadEquivalent : KeyInput -> KeyInput option 

