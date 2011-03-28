#light
namespace Vim

module PatternUtil =

    /// Is this a whole word pattern?
    let IsWholeWord pattern = StringUtil.startsWith "\<" pattern && StringUtil.endsWith "\>" pattern

    /// Will return the whole word being wrapped if this is a whole word pattern
    let GetUnderlyingWholeWord pattern = 
        if IsWholeWord pattern then
            Some (pattern.Substring(2, pattern.Length - 4))
        else
            None

    /// Create a whole word pattern
    let CreateWholeWord pattern = "\<" + pattern + "\>"
