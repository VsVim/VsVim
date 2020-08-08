namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining


module internal CommonUtil =

    val RaiseSearchResultMessage: IStatusUtil -> SearchResult -> unit
