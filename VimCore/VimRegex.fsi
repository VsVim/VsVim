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
    member IsMatch : pattern:string -> bool

    /// Matches the regex against the specified input and does the replacement 
    /// as specified.  If there is currently no regex then None will be returned
    member Replace : input:string -> replacement:string -> string 

[<Sealed>]
type VimRegexFactory = 

    new : IVimGlobalSettings -> VimRegexFactory

    member Create : string -> VimRegex
