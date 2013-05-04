#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods

type internal GlobalSettings = 
    interface IVimGlobalSettings
    new : unit -> GlobalSettings

type internal LocalSettings = 
    interface IVimLocalSettings
    new : IVimGlobalSettings -> LocalSettings

    static member Copy : IVimLocalSettings -> IVimLocalSettings

type internal WindowSettings = 
    interface IVimWindowSettings
    new : IVimGlobalSettings -> WindowSettings
    new : IVimGlobalSettings * ITextView -> WindowSettings

    static member Copy : IVimWindowSettings -> IVimWindowSettings


