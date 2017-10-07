#light

// Types that are shared between Vim and EditorUtils that need a temporary home
// while we are porting.
namespace Vim.ToDelete

type Contract = 

    static member Requires test = 
        if not test then
            raise (System.Exception("Contract failed"))

    [<System.Diagnostics.Conditional("DEBUG")>]
    static member Assert test = 
        if not test then
            raise (System.Exception("Contract failed"))

    static member GetInvalidEnumException<'T> (value : 'T) : System.Exception =
        let msg = sprintf "The value %O is not a valid member of type %O" value typedefof<'T>
        System.Exception(msg)

    static member FailEnumValue<'T> (value : 'T) : unit = 
        raise (Contract.GetInvalidEnumException value)

