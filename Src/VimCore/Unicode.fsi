#light

namespace Vim

/// Utility for getting unicode information.
module UnicodeUtil = 

    val IsWideBmp: codePoint: int -> bool

    val IsWideAstral: codePoint: int -> bool

    val IsWide: codePoint: int -> bool
