#light

namespace Vim

/// Represents a Vim style regular expression 
[<Sealed>]
type VimRegex =

    /// Text of the Regular expression
    member Text : string

    /// Does the string match the text
    member IsMatch : string -> bool

[<Sealed>]
type VimRegexFactory = 

    new : IVimGlobalSettings -> VimRegexFactory

    member Create : string -> VimRegex
