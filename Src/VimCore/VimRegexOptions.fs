namespace Vim

open System
open System.Text.RegularExpressions

/// Options which can be passed to a vim regex.  These can be overriden by
/// any embedded specifiers in the pattern.  For example IgnoreCase can
/// be overridden with a \C in the pattern.
///
/// The defaults here are the same as Vim.  case specific + magic
[<System.Flags>]
type VimRegexOptions =
    | Default = 0

    | NotCompiled = 1

    /// Causes the regex to ignore case.  This will override any embedded \C
    /// modifier in the pattern or a noignore case option
    | IgnoreCase = 2

    /// The case sensitivity is based on the pattern being provided.  If there
    /// are any upper case characters in the pattern it is case sensitive.  Else
    /// it falls back to the IgnoreCase option
    | SmartCase = 4

    /// Causes the regex to begin in nomagic mode.  This can be disabled later in
    /// the regex with a \m specifier
    | NoMagic = 8

/// Case specifier found in the regex (\c, \C or nothing)
[<RequireQualifiedAccess>]
[<NoComparison>]
type CaseSpecifier =

    /// Neither \c or \C was specified
    | None

    /// Pattern contained the \c modifier
    | IgnoreCase

    /// Pattern contained the \C modifier
    | OrdinalCase

[<RequireQualifiedAccess>]
[<NoComparison>]
type VimRegexReplaceCount =
    | One
    | All

/// Data for a replace operation
type VimRegexReplaceData =
    {

      /// The replacement of any previous substitute command
      PreviousReplacement: string

      /// When the '\r' replace sequence is used what should the replace string be.  This
      /// is usually contextual to the point in the IVimBuffer
      NewLine: string

      /// Whether or not magic should apply
      Magic: bool

      /// The 'count' times it should be replaced.  Not considered in a replace all
      Count: VimRegexReplaceCount }

    static member Default =
        { PreviousReplacement = ""
          NewLine = Environment.NewLine
          Magic = false
          Count = VimRegexReplaceCount.One }
