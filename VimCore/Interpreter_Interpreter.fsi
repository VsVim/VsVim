#light
namespace Vim.Interpreter
open Vim
open Microsoft.VisualStudio.Text

/// Engine which interprets Vim commands and expressions
[<Sealed>]
[<Class>]
type Interpreter =

    new : vimBuffer : IVimBuffer * commonOperations : ICommonOperations * foldManager : IFoldManager * fileSystem : IFileSystem-> Interpreter

    /// Get the ITextSnapshotLine for the provided LineSpecifier if it's 
    /// applicable
    member GetLine : lineSpecifier : LineSpecifier -> ITextSnapshotLine option

    /// Get the specified LineRange in the IVimBuffer
    member GetLineRange : lineRange : Vim.Interpreter.LineRange -> SnapshotLineRange option

    /// Run the LineCommand
    member RunLineCommand : lineCommand : LineCommand -> RunResult

    /// Run the Expression
    member RunExpression : expression : Expression -> Value

