#light

namespace Vim

module internal StringUtil =

    val FindFirst : seq<char> -> int -> (char -> bool) -> option<int * char>
    val CharAtOption : string -> int -> option<char>
    val CharAt : string -> int -> char
    val IsValidIndex : string -> int -> bool
    val Repeat : string -> int -> string
    val OfCharArray : char[] -> string
    val OfCharSeq : char seq -> string
    
