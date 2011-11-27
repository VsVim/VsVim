namespace Vim

[<Class>]
type LineRangeVisited =

    new : unit -> LineRangeVisited

    member Add : lineRange : LineRange -> unit

    member Contains : lineRange : LineRange -> bool

    /// Get the LineRange inside the provided LineRange which is not yet visited
    member GetUnvisited : lineRange : LineRange -> LineRange option
