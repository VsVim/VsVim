#light

namespace Vim
open System.Text.RegularExpressions

/// Represents a Vim style regular expression 
[<Sealed>]
type VimRegex =

    /// Text of the Regular expression
    member Text : string

    /// The underlying Regex expression.  Can be None if the original
    /// input string was an invalid pattern
    member Regex : Regex option

    /// Does the string match the text
    member IsMatch : string -> bool

[<Sealed>]
type VimRegexFactory = 

    new : IVimGlobalSettings -> VimRegexFactory

    member Create : string -> VimRegex
