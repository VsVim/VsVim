#light

namespace Vim

[<Class>]
type internal HistoryUtil = 

    static member Begin<'TData, 'TResult> : IHistoryClient<'TData, 'TResult> -> 'TData -> string -> BindDataStorage<'TResult>

