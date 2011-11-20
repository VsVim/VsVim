namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining

type internal InsertUtil =

    new : IVimBufferData * ICommonOperations -> InsertUtil

    interface IInsertUtil

