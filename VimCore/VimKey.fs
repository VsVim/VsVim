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

[<System.Flags>]
type KeyModifiers = 
    | None = 0x0
    | Alt = 0x1
    | Control = 0x2
    | Shift = 0x4

