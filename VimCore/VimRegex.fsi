#light

namespace Vim
open System.Text.RegularExpressions

/// Options which can be passed to a vim regex.  These override anything 
/// which is found embedded in the regex.  For example IgnoreCase will 
/// override an embedded \C in the pattern or a noignorecase option
[<System.Flags>]
type VimRegexOptions = 
    | None = 0
    | Compiled = 0x1
    | IgnoreCase = 0x2
    | OrdinalCase = 0x4

/// Represents a Vim style regular expression 
[<Sealed>]
type VimRegex =

    /// Text of the Regular expression
    member Text : string

    /// The underlying BCL Regex expression.  
    member Regex : Regex

    /// Does the string match the text
    member IsMatch : pattern:string -> bool

    /// Matches the regex against the specified input and does the replacement 
    /// as specified "count" times
    member Replace : input:string -> replacement:string -> count:int -> string 

    /// Matches the regex against the specified input and does the replacement 
    /// as specified.  If there is currently no regex then None will be returned
    member ReplaceAll : input:string -> replacement:string -> string 

[<Sealed>]
type VimRegexFactory = 

    new : IVimGlobalSettings -> VimRegexFactory

    member Create : pattern:string -> VimRegex option

    member CreateWithOptions : pattern:string -> options:VimRegexOptions -> VimRegex Option
