#light

namespace Vim

/// Utility class for converting Key notations into KeyInput and vice versa
/// :help key-notation for all of the key codes
module KeyNotationUtil =

    /// Try to convert the passed in string into a single KeyInput value according to the
    /// guidelines specified in :help key-notation.  
    val TryStringToKeyInput : string -> KeyInput option

    val StringToKeyInput : string -> KeyInput

    /// Try to convert the passed in string to multiple KeyInput values.  Returns true only
    /// if the entire list succesfully parses
    val TryStringToKeyInputSet : string -> KeyInputSet option

    /// Convert tho String to a KeyInputSet 
    val StringToKeyInputSet : string -> KeyInputSet

