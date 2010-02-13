#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input

/// Settings that can occur for a Vim program
type VimSettings = {
    IgnoreCase : bool;
    ShiftWidth : int;
    Scroll : option<int>;
    DisableCommand: KeyInput;
    }
    
module internal VimSettingsUtil =

    /// Create the default settings class
    let CreateDefault : VimSettings = {
        IgnoreCase = true;
        ShiftWidth = 4;
        Scroll = None;
        DisableCommand = KeyInput(System.Char.MinValue, Key.F12, ModifierKeys.Control ||| ModifierKeys.Shift);
        }    

    /// Get the scroll line count.  
    /// TODO: Actually find the count of visible lines
    let GetScrollLineCount (set:VimSettings) (view:ITextView) =
        match set.Scroll with
        | Some s -> s
        | None -> 50
            
        
