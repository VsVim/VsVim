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

