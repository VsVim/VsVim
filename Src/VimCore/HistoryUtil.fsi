#light

namespace Vim

[<Class>]
type internal HistoryUtil = 

    /// Begin a history related operation
    static member CreateHistorySession<'TData, 'TResult> : IHistoryClient<'TData, 'TResult> -> 'TData -> string -> IHistorySession<'TData, 'TResult>

    /// The set of KeyInput values which history considers to be a valid command
    static member CommandNames : KeyInput list
