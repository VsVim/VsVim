
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining

type internal CommandUtil =

    new : ITextView * Modes.ICommonOperations * ITextViewMotionUtil * IStatusUtil * IRegisterMap * IMarkMap * IVimData * IVimLocalSettings * IUndoRedoOperations-> CommandUtil

    interface ICommandUtil
