#light

namespace Vim

/// Provides values for the well known key values used by Vim 
type VimKey =
    | NotWellKnown = 0
    | Back = 1
    | Tab = 2
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
    | A = 44
    | B = 45 
    | C = 46 
    | D = 47 
    | E = 48 
    | F = 49 
    | G = 50 
    | H = 51 
    | I = 52 
    | J = 53 
    | K = 54 
    | L = 55 
    | M = 56 
    | N = 57 
    | O = 58 
    | P = 59 
    | Q = 60 
    | R = 61 
    | S = 62 
    | T = 63 
    | U = 64 
    | V = 65 
    | W = 66 
    | X = 67 
    | Y = 68 
    | Z = 69 
    | Number0 = 70
    | Number1 = 71
    | Number2 = 72
    | Number3 = 73
    | Number4 = 74
    | Number5 = 75
    | Number6 = 76
    | Number7 = 77
    | Number8 = 78
    | Number9 = 79
    | Bang = 80         // !
    | AtSign = 81       // @
    | Pound = 82        // #
    | Percent = 83      // %
    | Caret = 84        // ^
    | Ampersand = 85    // &
    | Asterick = 86     // *
    | OpenParen = 87    // (
    | CloseParen = 88   // )
    | OpenBracket = 89  // [
    | CloseBracket = 90 // ]
    | OpenBrace = 91    // {
    | CloseBrace = 92   // }
    | Minus = 93        // -
    | Underscore = 94   // _
    | Equals = 95       // =
    | Backslash = 96    // \
    | Forwardslash = 97 // /
    | Plus = 98         // +
    | Pipe = 99         // |
    | SingleQuote = 100 // '
    | DoubleQuote = 101 // "
    | Backtick = 102    // `
    | Question = 103    // ?
    | Comma = 104       // ,
    | LessThan = 105    // <
    | GreaterThan = 106 // >
    | Period = 107      // .
    | Semicolon = 108   // ;
    | Colon = 109       // :
    | Tilde = 110       // ~
    | Space = 111       //  
    | Dollar = 112      // $

module VimKeyUtil =

    /// Is this a key from the Keypad
    let IsKeypadKey key = 
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
        | VimKey.KeypadPlus -> true
        | VimKey.KeypadMinus -> true
        | VimKey.KeypadDecimal -> true
        | VimKey.KeypadDivide -> true
        | VimKey.KeypadMultiply -> true
        | _ -> false

[<System.Flags>]
type KeyModifiers = 
    | None = 0x0
    | Alt = 0x1
    | Control = 0x2
    | Shift = 0x4

