#light

namespace VimCore.Modes.Normal
open VimCore
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media

module internal Operations =

    let All : list<Operation> = list.Empty

