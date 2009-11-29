#light

namespace VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

/// Settings that can occur for a Vim program
type VimSettings = {
    IgnoreCase : bool;
    ShiftWidth : int;
    Scroll : option<int>;
    }
    
module internal VimSettingsUtil =

    /// Create the default settings class
    let CreateDefault : VimSettings = {
        IgnoreCase = true;
        ShiftWidth = 4;
        Scroll = None
        
        }    

    /// Get the scroll line count.  
    /// TODO: Actually find the count of visible lines
    let GetScrollLineCount (set:VimSettings) (view:IWpfTextView) =
        match set.Scroll with
        | Some s -> s
        | None -> 50
            
        
