namespace Vim

open System.Text.RegularExpressions


/// The Case Specified for this VimRegex

/// Represents a Vim style regular expression
[<Sealed>]
type VimRegex =
    member CaseSpecifier: CaseSpecifier

    /// The pattern includes a new line reference (\n not $).
    member IncludesNewLine: bool

    /// The pattern matches the visual selection.
    member MatchesVisualSelection: bool

    /// Pattern of the BCL version of the regular expression
    member RegexPattern: string

    /// The underlying BCL Regex expression.
    member Regex: Regex

    /// Vim Pattern of the Regular expression
    member VimPattern: string

    /// Does the string match the text
    member IsMatch: pattern:string -> bool

    /// Matches the regex against the specified input and does the replacement
    /// as specified.  If there is currently no regex then None will be returned
    member Replace: input:string
         -> replacement:string -> replaceData:VimRegexReplaceData -> registerMap:IRegisterMap -> string

module VimRegexFactory =

    val Create: pattern:string -> options:VimRegexOptions -> VimRegex option

    val CreateEx: pattern:string -> options:VimRegexOptions -> VimResult<VimRegex>

    val CreateForSettings: pattern:string -> globalSettings:IVimGlobalSettings -> VimRegex option

    val CreateForSubstituteFlags: pattern:string
         -> globalSettings:IVimGlobalSettings -> flags:SubstituteFlags -> VimRegex option

    val CreateRegexOptions: globalSettings:IVimGlobalSettings -> VimRegexOptions

    val CreateBcl: pattern:string -> regexOptions:RegexOptions -> Regex option
