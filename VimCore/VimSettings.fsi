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
    new : IVimGlobalSettings * IEditorOptions -> LocalSettings
    new : IVimGlobalSettings * IEditorOptions * ITextView -> LocalSettings

    static member Copy : IVimLocalSettings -> IVimLocalSettings



