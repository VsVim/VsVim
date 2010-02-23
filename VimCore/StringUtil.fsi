#light

namespace Vim

module internal StringUtil =

    val FindFirst : seq<char> -> int -> (char -> bool) -> option<int * char>
    val CharAtOption : string -> int -> option<char>
    val CharAt : string -> int -> char
    val IsValidIndex : string -> int -> bool
    val Repeat : string -> int -> string

    /// Create a String from an array of chars
    val OfCharArray : char[] -> string

    /// Create a String from a sequence of chars
    val OfCharSeq : char seq -> string

    /// Returns the length of the string.  0 is returned for null values
    val Length : string -> int
    
