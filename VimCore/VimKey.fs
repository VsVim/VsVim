#light

namespace Vim

/// Provides values for the well known key values used by Vim 
type VimKey =
    | None = 0
    | Back = 1
    | FormFeed = 2
    | Enter = 3
    | Escape = 4
    | Left = 5
    | Up = 6
    | Right = 7
    | Down = 8
    | Delete = 9
    | Help = 10
    | End = 11
    | PageUp = 12
    | PageDown = 13
    | Insert = 14
    | Home = 15
    | Break = 16
    | F1 = 17
    | F2 = 18
    | F3 = 19
    | F4 = 20
    | F5 = 21
    | F6 = 22
    | F7 = 23
    | F8 = 24
    | F9 = 25
    | F10 = 26
    | F11 = 27
    | F12 = 28
    | KeypadDecimal = 29
    | Keypad0 = 30
    | Keypad1 = 31
    | Keypad2 = 32
    | Keypad3 = 33
    | Keypad4 = 34
    | Keypad5 = 35
    | Keypad6 = 36
    | Keypad7 = 37
    | Keypad8 = 38
    | Keypad9 = 39
    | KeypadPlus = 40
    | KeypadMinus = 41
    | KeypadDivide = 42
    | KeypadMultiply = 43
    | LowerA = 44
    | LowerB = 45 
    | LowerC = 46 
    | LowerD = 47 
    | LowerE = 48 
    | LowerF = 49 
    | LowerG = 50 
    | LowerH = 51 
    | LowerI = 52 
    | LowerJ = 53 
    | LowerK = 54 
    | LowerL = 55 
    | LowerM = 56 
    | LowerN = 57 
    | LowerO = 58 
    | LowerP = 59 
    | LowerQ = 60 
    | LowerR = 61 
    | LowerS = 62 
    | LowerT = 63 
    | LowerU = 64 
    | LowerV = 65 
    | LowerW = 66 
    | LowerX = 67 
    | LowerY = 68 
    | LowerZ = 69 
    | UpperA = 70 
    | UpperB = 71 
    | UpperC = 72 
    | UpperD = 73 
    | UpperE = 74 
    | UpperF = 75 
    | UpperG = 76 
    | UpperH = 77 
    | UpperI = 78 
    | UpperJ = 79 
    | UpperK = 80 
    | UpperL = 81 
    | UpperM = 82 
    | UpperN = 83 
    | UpperO = 84 
    | UpperP = 85 
    | UpperQ = 86 
    | UpperR = 87 
    | UpperS = 88 
    | UpperT = 89 
    | UpperU = 90 
    | UpperV = 91 
    | UpperW = 92 
    | UpperX = 93 
    | UpperY = 94 
    | UpperZ = 95 
    | Number0 = 96 
    | Number1 = 97 
    | Number2 = 98 
    | Number3 = 99
    | Number4 = 100
    | Number5 = 101
    | Number6 = 102
    | Number7 = 103
    | Number8 = 104
    | Number9 = 105
    | Bang = 106            // !
    | AtSign = 107          // @
    | Pound = 108           // #
    | Percent = 109         // %
    | Caret = 110           // ^
    | Ampersand = 111       // &
    | Asterick = 112        // *
    | OpenParen = 113       // (
    | CloseParen = 114      // )
    | OpenBracket = 115     // [
    | CloseBracket = 116    // ]
    | OpenBrace = 117       // {
    | CloseBrace = 118      // }
    | Minus = 119           // -
    | Underscore = 120      // _
    | Equals = 121          // =
    | Backslash = 122       // \
    | Forwardslash = 123    // /
    | Plus = 124            // +
    | Pipe = 125            // |
    | SingleQuote = 126     // '
    | DoubleQuote = 127     // "
    | Backtick = 128        // `
    | Question = 129        // ?
    | Comma = 130           // ,
    | LessThan = 131        // <
    | GreaterThan = 132     // >
    | Period = 133          // .
    | Semicolon = 134       // ;
    | Colon = 135           // :
    | Tilde = 136           // ~
    | Space = 137           //  
    | Dollar = 138          // $
    | Tab = 139
    | LineFeed = 140
    | Nop = 141             // no-op.  Does nothing
    | Null = 142            // (char)0

    // The character is real it's simply an unknown quantity to Vim
    | RawCharacter = 143

module VimKeyUtil =

    /// Is this a number key from the Keypad
    let IsKeypadNumberKey key = 
        match key with
        | VimKey.Keypad0 -> true
        | VimKey.Keypad1 -> true
        | VimKey.Keypad2 -> true
        | VimKey.Keypad3 -> true
        | VimKey.Keypad4 -> true
        | VimKey.Keypad5 -> true
        | VimKey.Keypad6 -> true
        | VimKey.Keypad7 -> true
        | VimKey.Keypad8 -> true
        | VimKey.Keypad9 -> true
        | _ -> false

    /// Is this a key from the Keypad
    let IsKeypadKey key = 
        if IsKeypadNumberKey key then
            true
        else
            match key with
            | VimKey.KeypadPlus -> true
            | VimKey.KeypadMinus -> true
            | VimKey.KeypadDecimal -> true
            | VimKey.KeypadDivide -> true
            | VimKey.KeypadMultiply -> true
            | _ -> false

[<System.Flags>]
type KeyModifiers = 
    | None = 0x0

    /// The Alt or Meta Key 
    | Alt = 0x1

    /// The Control key
    | Control = 0x2

    /// The Shift key
    | Shift = 0x4

    /// The Command key.  This isn't actually used in VsVim but is a place holder 
    /// for the command key notation <D-...> which is only valid on Mac's
    | Command = 0x8

