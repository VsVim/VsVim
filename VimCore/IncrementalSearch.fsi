#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining

type internal IncrementalSearch =
    new : Modes.ICommonOperations * IVimLocalSettings * ITextStructureNavigator * IStatusUtil * IVimData -> IncrementalSearch

    interface IIncrementalSearch


