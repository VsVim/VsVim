#light

namespace Vim
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Formatting
open System
open System.Runtime.InteropServices
open System.Windows.Threading
open Microsoft.VisualStudio.Text.Classification

type internal CaretData = {
    /// Image being used to draw the caret
    Image : Image;

    /// Color used to create the brush.  Can be empty in the case we can't determine the color
    Color : option<Color>;

    /// Point this caret is tracking
    Point : SnapshotPoint;
}

/// Standard implementation of IBlockCaret which draws a block caret vs. the pipe style caret
/// which is used in several Vim modes
type internal BlockCaret
    (
        _view : ITextView ) =
    let _caretOpacity = 0.65

    /// Tag used to identify our items in the adornment layer 
    let _tag = Object()

    let mutable _blinkTimer : DispatcherTimer = null

    /// Information about the caret, will be empty if the caret is not currently being displayed
    let mutable _caretData : option<CaretData> = None
    
    /// Does the consumer of IBlock caret want us to be in control of displaying the caret
    let mutable _isShown : bool = false

    interface IBlockCaret with
        member x.TextView = _view 
        member x.IsShown = _isShown
        member x.Hide() = _isShown <- false
        member x.Show() = _isShown <- true
        member x.Destroy() = ()


