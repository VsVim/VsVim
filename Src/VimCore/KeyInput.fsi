
#light

namespace Vim

/// Represents a key input by the user.  This mapping is independent of keyboard 
/// layout and simply represents Vim's view of key input
[<Sealed>]
type KeyInput =

    /// The character representation of this input.  If there is no character representation
    /// then Char.MinValue will be returend
    member Char: char

    /// Returns the actual backing character in the form of an option.  Several keys 
    /// don't have corresponding char values and will return None
    member RawChar: char option

    /// The VimKey for this KeyInput.  
    member Key: VimKey

    /// The extra modifier keys applied to the VimKey value
    member KeyModifiers: VimKeyModifiers

    /// Whether the key has any key modifiers
    member HasKeyModifiers: bool

    /// Is the character for this KeyInput a digit
    member IsDigit: bool

    /// Is this an arrow key?
    member IsArrowKey: bool 

    /// Is this a function key
    member IsFunctionKey: bool

    /// Is this a mouse key
    member IsMouseKey: bool

    /// Compare to another KeyInput value
    member CompareTo: other: KeyInput -> int

    /// The empty KeyInput.  Used in places where a KeyInput is required but no 
    /// good mapping exists
    static member DefaultValue: KeyInput

    static member op_Equality: KeyInput * KeyInput -> bool
    static member op_Inequality: KeyInput * KeyInput -> bool

    interface System.IComparable
    interface System.IComparable<KeyInput>
    interface System.IEquatable<KeyInput>

/// Represents a key input together with whether the key input was the result
/// of a mapping
[<Sealed>]
type KeyInputData =
    member KeyInput: KeyInput
    member WasMapped: bool
    static member Create: keyInput: KeyInput -> wasMapped: bool -> KeyInputData

module KeyInputUtil = 

    /// The Null Key: VimKey.Null
    val NullKey: KeyInput

    /// The LineFeed key: VimKey.LineFeed
    val LineFeedKey: KeyInput

    /// The FormFeed key: VimKey.FormFeed
    val FormFeedKey: KeyInput

    /// The Enter Key: VimKey.Enter
    val EnterKey: KeyInput

    /// The Escape Key: VimKey.Escape
    val EscapeKey: KeyInput 

    /// The Tab Key: VimKey.Tab
    val TabKey: KeyInput 

    /// The KeyInput for every VimKey in the system which is considered predefined
    val VimKeyInputList: KeyInput list

    /// The set of core characters
    val VimKeyCharSet: char Set

    /// The set of core characters as a list
    val VimKeyCharList: char list

    /// Apply the modifiers to the given KeyInput and determine the result.  This will
    /// not necessarily return a KeyInput with the modifier set.  It attempts to unify 
    /// certain ambiguous combinations.
    val ApplyKeyModifiers: keyInput: KeyInput -> modifiers: VimKeyModifiers -> KeyInput

    /// Apply the modifiers to the given character
    val ApplyKeyModifiersToChar: c: char  -> modifiers: VimKeyModifiers -> KeyInput

    /// Apply the modifiers to the given VimKey
    val ApplyKeyModifiersToKey: vimKey: VimKey -> modifiers: VimKeyModifiers -> KeyInput

    /// Apply a click count to the given key input
    val ApplyClickCount: keyInput: KeyInput -> clickCount: int -> KeyInput

    /// Normalize key modifiers, e.g. removing an irrelevant shift
    val NormalizeKeyModifiers: keyInput: KeyInput -> KeyInput

    /// Try and convert the given char to a KeyInput value
    val CharToKeyInput: c: char -> KeyInput

    /// Convert the passed in char to a KeyInput with Control
    val CharWithControlToKeyInput: c: char -> KeyInput

    /// Convert the passed in char to a KeyInput with Alt
    val CharWithAltToKeyInput: c: char -> KeyInput

    /// Convert the specified VimKey code to a KeyInput 
    val VimKeyToKeyInput: vimKey: VimKey -> KeyInput

    /// Change the KeyModifiers associated with this KeyInput.  Will not change the value
    /// of the underlying char.  Although it may produce a KeyInput that makes no 
    /// sense.  For example it's very possible to have KeyInput('a', VimKeyModifiers.Shift) but
    /// it will be extremely hard to produce that in a keyboard (if possible at all).  
    ///
    /// This method should be avoided.  If you need to apply modifiers then use
    /// ApplyModifiers which uses Vim semantics when deciding how to apply the modifiers
    val ChangeKeyModifiersDangerous: keyInput: KeyInput -> modifiers: VimKeyModifiers -> KeyInput

    /// Get the alternate key for the given KeyInput if it's a key from the keypad 
    val GetNonKeypadEquivalent: keyInput: KeyInput -> KeyInput option 

    /// Whether this key input could appear in vim's standard bindings
    val IsCore: keyInput: KeyInput -> bool

