#light

namespace Vim
open System.Text.RegularExpressions

/// Options which can be passed to a vim regex.  These can be overriden by
/// any embedded specifiers in the pattern.  For example IgnoreCase can
/// be overridden with a \C in the pattern.  
///
/// The defaults here are the same as Vim.  case specific + magic
[<System.Flags>]
type VimRegexOptions = 
    | Default = 0

    | NotCompiled = 0x1

    /// Causes the regex to ignore case.  This will override any embedded \C 
    /// modifier in the pattern or a noignore case option 
    | IgnoreCase = 0x2

    /// The case sensitivity is based on the pattern being provided.  If there
    /// are any upper case characters in the pattern it is case sensitive.  Else
    /// it falls back to the IgnoreCase option
    | SmartCase = 0x4

    /// Causes the regex to begin in nomagic mode.  This can be disabled later in
    /// the regex with a \m specifier
    | NoMagic = 0x8

/// Case specifier found in the rege (\c, \C or nothing)
[<RequireQualifiedAccess>]
[<NoComparison>]
type CaseSpecifier =

    /// Neither \c or \C was specified
    | None

    /// Pattern contained the \c modifier
    | IgnoreCase

    /// Pattern contained the \C modifier
    | OrdinalCase 

/// Data for a replace operation
type ReplaceData = {

    /// When the '\r' replace sequence is used what should the replace string be.  This
    /// is usually contextual to the point in the IVimBuffer
    NewLine : string

    /// Whether or not magic should apply
    Magic : bool

    /// The 'count' times it should be replaced.  Not considered in a replace all
    Count : int
}

/// Represents a Vim style regular expression 
[<Sealed>]
type VimRegex =

    /// The Case Specified for this VimRegex
    member CaseSpecifier : CaseSpecifier

    /// The pattern includes a new line reference (\n not $).  
    member IncludesNewLine : bool

    /// Pattern of the BCL version of the regular expression
    member RegexPattern : string

    /// The underlying BCL Regex expression.  
    member Regex : Regex

    /// Vim Pattern of the Regular expression
    member VimPattern : string

    /// Does the string match the text
    member IsMatch : pattern : string -> bool

    /// Matches the regex against the specified input and does the replacement 
    /// as specified "count" times
    member Replace : input : string -> replacement : string -> replaceData : ReplaceData -> string 

    /// Matches the regex against the specified input and does the replacement 
    /// as specified.  If there is currently no regex then None will be returned
    member ReplaceAll : input : string -> replacement : string -> replaceData : ReplaceData -> string 

module VimRegexFactory = 

    val Create : pattern : string -> options : VimRegexOptions -> VimRegex option

    val CreateForSettings : pattern : string -> globalSettings : IVimGlobalSettings -> VimRegex option

    val CreateForSubstituteFlags : pattern : string -> globalSettings : IVimGlobalSettings -> flags : SubstituteFlags -> VimRegex option

    val CreateRegexOptions : globalSettings : IVimGlobalSettings -> VimRegexOptions

    val CreateBcl : pattern : string -> regexOptions : RegexOptions -> Regex option




