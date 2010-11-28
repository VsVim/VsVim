namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal SearchService =
    new : ITextSearchService * IVimGlobalSettings -> SearchService

    interface ISearchService

