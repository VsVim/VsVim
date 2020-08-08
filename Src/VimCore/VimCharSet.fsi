namespace Vim


/// The original text the VimCharSet was parsed from

/// Represents a collection of characters. This format for the character collection
/// is described in `:help ifsname`
[<Class>]
[<Sealed>]
type VimCharSet =
    member Text: string

    /// Is the character included in this set
    member Contains: c:char -> bool

    static member TryParse: text:string -> VimCharSet option
