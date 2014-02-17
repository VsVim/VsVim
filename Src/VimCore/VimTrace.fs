#light
namespace Vim
open System
open System.Diagnostics

[<AbstractClass>]
type VimTrace() =

    static let _prefixInfo = "VsVim "
    static let _prefixError = "VsVim Error "
    static let _traceSwitch = TraceSwitch("VsVim", "VsVim Trace")

    static member TraceSwitch = _traceSwitch

    [<Conditional("TRACE")>]
    static member TraceInfo(msg : string) = 
        let msg = _prefixInfo + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceInfo, msg)

    [<Conditional("TRACE")>]
    static member TraceInfo(format : string, [<ParamArrayAttribute>] args : obj []) = 
        let msg = String.Format(format, args)
        let msg = _prefixInfo + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceInfo, msg)

    [<Conditional("TRACE")>]
    static member TraceError(ex : Exception) = 
        let msg = ex.Message + Environment.NewLine + ex.StackTrace
        VimTrace.TraceError(msg)

    [<Conditional("TRACE")>]
    static member TraceError(msg : string) = 
        let msg = _prefixError + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceError, msg)

    [<Conditional("TRACE")>]
    static member TraceError(format : string, [<ParamArrayAttribute>] args : obj []) = 
        let msg = String.Format(format, args)
        let msg = _prefixError + msg
        Trace.WriteLineIf(VimTrace.TraceSwitch.TraceError, msg)


