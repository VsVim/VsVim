#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open System.Text.RegularExpressions
open Vim.RegexUtil

type internal CommandProcessor = 
    new : IVimBuffer * IOperations * IStatusUtil * IFileSystem -> CommandProcessor

    interface ICommandProcessor
