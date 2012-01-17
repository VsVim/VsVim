#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open System.Text.RegularExpressions

// TODO:  Delete.  Doesn't need to exist anymore
type internal CommandProcessor = 
    new : IVimBuffer * ICommonOperations * IFileSystem * IFoldManager * IBufferTrackingService -> CommandProcessor

    interface ICommandProcessor
