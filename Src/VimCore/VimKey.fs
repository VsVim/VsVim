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
    | KeypadEnter = 44
    | Nop = 45              // no-op.  Does nothing
    | Null = 46             // (char)0
    | LineFeed = 47
    | Tab = 48
    | RawCharacter = 50     // A simple character to be processed
    | LeftMouse = 51
    | LeftDrag = 52
    | LeftRelease = 53
    | MiddleMouse = 54
    | MiddleDrag = 55
    | MiddleRelease = 56
    | RightMouse = 57
    | RightDrag = 58
    | RightRelease = 59
    | X1Mouse = 60
    | X1Drag = 61
    | X1Release = 62
    | X2Mouse = 63
    | X2Drag = 64
    | X2Release = 65

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

    /// Is this an arrow key?
    let IsArrowKey key = 
        match key with
        | VimKey.Left -> true
        | VimKey.Right -> true
        | VimKey.Up -> true
        | VimKey.Down -> true
        | _ -> false

    /// Is this a function key
    let IsFunctionKey key =    
        match key with
        | VimKey.F1 -> true
        | VimKey.F2 -> true
        | VimKey.F3 -> true
        | VimKey.F4 -> true
        | VimKey.F5 -> true
        | VimKey.F6 -> true
        | VimKey.F7 -> true
        | VimKey.F8 -> true
        | VimKey.F9 -> true
        | VimKey.F10 -> true
        | VimKey.F11 -> true
        | VimKey.F12 -> true
        | _ -> false

    /// Is this a mouse key
    let IsMouseKey key =
        match key with
        | VimKey.LeftMouse -> true
        | VimKey.LeftRelease -> true
        | VimKey.LeftDrag -> true
        | VimKey.RightMouse -> true
        | VimKey.RightRelease -> true
        | VimKey.RightDrag -> true
        | VimKey.MiddleMouse -> true
        | VimKey.MiddleRelease -> true
        | VimKey.MiddleDrag -> true
        | VimKey.X1Mouse -> true
        | VimKey.X1Release -> true
        | VimKey.X1Drag -> true
        | VimKey.X2Mouse -> true
        | VimKey.X2Release -> true
        | VimKey.X2Drag -> true
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

